using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Framing;
using RestBus.Client;
using RestBus.Common;
using RestBus.Common.Amqp;
using RestBus.Common.Http;
using RestBus.RabbitMQ.ChannelPooling;
using RestBus.RabbitMQ.Consumer;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace RestBus.RabbitMQ.Client
{

    public class RestBusClient : MessageInvokerBase
    {
        static SequenceGenerator correlationIdGen = SequenceGenerator.FromUtcNow();

        readonly IMessageMapper messageMapper;
        readonly ExchangeInfo exchangeInfo;
        readonly string clientId;
        readonly string exchangeName;
        readonly string callbackQueueName;
        readonly ConnectionFactory connectionFactory;
        ConcurrentQueueingConsumer callbackConsumer;
        IConnection conn;
        AmqpChannelPooler _clientPool;
        volatile bool isInConsumerLoop;
        volatile bool consumerCancelled;
        event Action<BasicDeliverEventArgs> responseArrivalNotification;

        readonly object callbackConsumerStartSync = new object();
        object exchangeDeclareSync = new object();
        int lastExchangeDeclareTickCount = 0;
        volatile bool disposed = false;

        volatile bool hasKickStarted = false;
        private Uri baseAddress;
        private HttpRequestHeaders defaultRequestHeaders;
        private TimeSpan timeout;

        internal const int HEART_BEAT = 30;
        static readonly RabbitMQMessagingProperties _defaultMessagingProperties = new RabbitMQMessagingProperties();

        /// <summary>Initializes a new instance of the <see cref="T:RestBus.RabbitMQ.RestBusClient" /> class.</summary>
        public RestBusClient(IMessageMapper messageMapper) : base(new HttpClientHandler(), true)
        {
            //Set default HttpClient related fields
            timeout = TimeSpan.FromSeconds(100);
            MaxResponseContentBufferSize = int.MaxValue;
            //TODO: Setup cancellation token here.

            //Configure RestBus fields/properties
            this.messageMapper = messageMapper;
            this.exchangeInfo = messageMapper.GetExchangeInfo();
            this.clientId = AmqpUtils.GetNewExclusiveQueueId();
            this.exchangeName = AmqpUtils.GetExchangeName(exchangeInfo);
            this.callbackQueueName = AmqpUtils.GetCallbackQueueName(exchangeInfo, clientId);

            //Map request to RabbitMQ Host and exchange, 
            this.connectionFactory = new ConnectionFactory();
            connectionFactory.Uri = exchangeInfo.ServerAddress;
            connectionFactory.RequestedHeartbeat = HEART_BEAT;
        }

        /// <summary>Gets or sets the base address of Uniform Resource Identifier (URI) of the Internet resource used when sending requests.</summary>
        /// <returns>Returns <see cref="T:System.Uri" />.The base address of Uniform Resource Identifier (URI) of the Internet resource used when sending requests.</returns>
        public Uri BaseAddress
        {
            get
            {
                return baseAddress;
            }
            set
            {
                EnsureNotStartedOrDisposed();
                baseAddress = value;
            }
        }

        /// <summary>Gets the headers which should be sent with each request.</summary>
        /// <returns>Returns <see cref="T:System.Net.Http.Headers.HttpRequestHeaders" />.The headers which should be sent with each request.</returns>
        public HttpRequestHeaders DefaultRequestHeaders
        {
            //HTTPRequestHeaders ctor is internal so this property cannot be instantiated by this class and so is useless ...sigh...
            //Fortunately, you can specify Headers per message when using the RequestOptions class

            //TODO: Consider throwing a NotSupported Exception here instead, since a caller will not expect null.
            get
            {
                return defaultRequestHeaders;
            }
        }

        /// <summary>Gets or sets the maximum number of bytes to buffer when reading the response content.</summary>
        /// <returns>Returns <see cref="T:System.Int32" />.The maximum number of bytes to buffer when reading the response content. The default value for this property is 64K.</returns>
        /// <exception cref="T:System.ArgumentOutOfRangeException">The size specified is less than or equal to zero.</exception>
        /// <exception cref="T:System.InvalidOperationException">An operation has already been started on the current instance. </exception>
        /// <exception cref="T:System.ObjectDisposedException">The current instance has been disposed. </exception>
        public long MaxResponseContentBufferSize
        {
            //Entire Message is dequeued from queue
            //So this property is only here for compatibilty with HttpClient and does nothing
            get;
            set;
        }

        /// <summary>Gets or sets the number of milliseconds to wait before the request times out.</summary>
        /// <returns>Returns <see cref="T:System.TimeSpan" />.The number of milliseconds to wait before the request times out.</returns>
        /// <exception cref="T:System.ArgumentOutOfRangeException">The timeout specified is less than zero and is not <see cref="F:System.Threading.Timeout.Infinite" />.</exception>
        /// <exception cref="T:System.InvalidOperationException">An operation has already been started on the current instance. </exception>
        /// <exception cref="T:System.ObjectDisposedException">The current instance has been disposed.</exception>
        public TimeSpan Timeout
        {
            get
            {
                return timeout;
            }
            set
            {
                if (value != System.Threading.Timeout.InfiniteTimeSpan  && value <= TimeSpan.Zero)
                {
                    throw new ArgumentOutOfRangeException("value");
                }
                EnsureNotStartedOrDisposed();
                timeout = value;
            }
        }

        /// <summary>Cancel all pending requests on this instance.</summary>
        public void CancelPendingRequests()
        {
            //TODO: Implement CancelPendingRequests() 
        }


        //TODO: Confirm that this method is thread safe

        /// <summary>Send an HTTP request as an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="request">The HTTP request message to send.</param>
        /// <param name="cancellationToken">The cancellation token to cancel operation.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="request" /> was null.</exception>
        public override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException("request");

            if (request.RequestUri == null && BaseAddress == null )
            {
               throw new InvalidOperationException("The request URI must either be set or BaseAddress must be set");
            }

            if (disposed) throw new ObjectDisposedException("Client has been disposed");
            hasKickStarted = true;
            PrepareMessage(request);

            //Get Request Options
            RequestOptions requestOptions = GetRequestOptions(request);
            var requestMessagingProperties = GetRequestOptionsMessagingProperties(requestOptions);

            //Declare messaging resources
            Action<BasicDeliverEventArgs> arrival = null;
            ManualResetEventSlim receivedEvent = null;
            AmqpModelContainer model = null;
            bool modelClosed = false;

            try
            {
                //Start Callback consumer if it hasn't started
                StartCallbackQueueConsumer();

                var pooler = _clientPool; //Henceforth, use pooler since _clientPool may change and we want to work with the original pooler

                //Test if conn or pooler is null, then leave
                if (conn == null || pooler == null)
                {
                    // This means a connection could not be created most likely because the server was Unreachable.
                    // This shouldn't happen because StartCallbackQueueConsumer should have thrown the exception

                    //TODO: The inner exception here is a good candidate for a RestBusException
                    throw GetWrappedException("Unable to establish a connection.", new ApplicationException("Unable to establish a connection."));
                }

                //TODO: Check if cancellation token was set before operation even began
                var taskSource = new TaskCompletionSource<HttpResponseMessage>();

                //NOTE: You're not supposed to share channels across threads but Iin this situation where only one thread can have access to a channel at a time, all's good.

                //TODO: Consider placing model acquisition/return in a try-finally block: Implement once this method has been simplified.
                model = pooler.GetModel(ChannelFlags.None);

                //Redeclare exchanges and queues every 30 seconds or the first time client is sending a message
                TimeSpan elapsedSinceLastDeclareExchange = TimeSpan.FromMilliseconds(Environment.TickCount - lastExchangeDeclareTickCount);
                bool firstDeclare = lastExchangeDeclareTickCount == 0;
                if (firstDeclare || elapsedSinceLastDeclareExchange.TotalMilliseconds < 0 || elapsedSinceLastDeclareExchange.TotalSeconds > 30)
                {
                    if (!firstDeclare)
                    {
                        //All threads must attempt to declare exchanges and queues if it hasn't been previously declared 
                        //(for instance, all threads were started at once)
                        //So do not swap out this value on first declare
                        Interlocked.Exchange(ref lastExchangeDeclareTickCount, Environment.TickCount);
                    }
                    AmqpUtils.DeclareExchangeAndQueues(model.Channel, exchangeInfo, exchangeDeclareSync, null);
                    if(firstDeclare)
                    {
                        //Swap out this value after declaring on firstdeclare
                        Interlocked.Exchange(ref lastExchangeDeclareTickCount, Environment.TickCount);
                    }
                }

                //TODO: if exchangeInfo wants a Session/Server/Sticky Queue
                //TODO: if exchangeInfo wants a Durable Queue

                string correlationId = correlationIdGen.GetNextId();
                BasicProperties basicProperties = new BasicProperties { CorrelationId = correlationId };

                //Set message delivery mode:

                //Make message persistent if either:
                // 1. Properties.Persistent is true
                // 2. MessageMapper.PersistentMessages is true and Properties.Persistent is null
                // 3. MessageMapper.PersistentMessages is true and Properties.Persistent is true
                if (requestMessagingProperties.Persistent == true || (messageMapper.PersistentMessages && requestMessagingProperties.Persistent != false))
                {
                    basicProperties.Persistent = true;
                }

                TimeSpan requestTimeout = GetRequestTimeout(requestOptions);

                if (requestTimeout != TimeSpan.Zero)
                {
                    basicProperties.ReplyTo = callbackQueueName;
                    if (!IsRequestTimeoutInfinite(requestOptions) && messageMapper.GetExpires(request))
                    {
                        if (requestTimeout.TotalMilliseconds > Int32.MaxValue)
                        {
                            basicProperties.Expiration = Int32.MaxValue.ToString();
                        }
                        else
                        {
                            basicProperties.Expiration = requestTimeout.TotalMilliseconds.ToString();
                        }
                    }

                    //Message arrival event
                    HttpResponsePacket responsePacket = null;
                    Exception deserializationException = null;
                    receivedEvent = new ManualResetEventSlim(false);

                    //TODO: Get rid of arrival below after turning responseArrivalNotification into a hashtable.
                    // The hashtable shouldn't store a delegate, it should an object that encapsulates
                    // responsePacket, deserializationException and receivedEvent.
                    // This will allow the callback consumer to process the arrival directly instead of calling a delegate.

                    arrival = a =>
                    {
                        if (a.BasicProperties.CorrelationId == correlationId)
                        {
                            HttpResponsePacket res = null;
                            try
                            {
                                res = HttpResponsePacket.Deserialize(a.Body);
                            }
                            catch (Exception ex)
                            {
                                Interlocked.Exchange(ref deserializationException, ex);
                            }

                            if (deserializationException == null)
                            {
                                if (res != null)
                                {
                                    //Add/Update Content-Length Header
                                    //TODO: Is there any need to add this here if it's subsequently removed/updated by TryGetHttpResponseMessage/HttpPacket.PopulateHeaders? (Is this useful in the exception/other path scenario?
                                    res.Headers["Content-Length"] = new string[] { (res.Content == null ? 0 : res.Content.Length).ToString() }; ;
                                }

                                Interlocked.Exchange(ref responsePacket, res);
                                responsePacket = res; //TODO: This looks like it's unnecessary due to line above
                            }

                            //NOTE: The ManualResetEventSlim.Set() method can be called after the object has been disposed
                            //So no worries about the Timeout disposing the object before the response comes in.
                            receivedEvent.Set();
                            responseArrivalNotification -= arrival;
                        }
                    };

                    if (!cancellationToken.Equals(System.Threading.CancellationToken.None))
                    {
                        //TODO: Have cancellationtokens cancel event trigger callbackHandle
                        //In fact turn this whole thing into an extension
                    }


                    //Wait for received event on the ThreadPool:

                    var localVariableInitLock = new object();

                    lock (localVariableInitLock)
                    {
                        RegisteredWaitHandle callbackHandle = null;
                        callbackHandle = ThreadPool.RegisterWaitForSingleObject(receivedEvent.WaitHandle,
                            (state, timedOut) =>
                            {
                                try
                                {
                                    //TODO: Check Cancelation Token when it's implemented
                                    if (timedOut)
                                    {
                                        //NOTE: This really ought to return an "Operation Timed Out" WebException and not a Cancellation as noted in the following posts
                                        // http://social.msdn.microsoft.com/Forums/en-US/d8d87789-0ac9-4294-84a0-91c9fa27e353/bug-in-httpclientgetasync-should-throw-webexception-not-taskcanceledexception?forum=netfxnetcom&prof=required
                                        // http://stackoverflow.com/questions/10547895/how-can-i-tell-when-httpclient-has-timed-out
                                        // and http://stackoverflow.com/questions/12666922/distinguish-timeout-from-user-cancellation
                                        //
                                        // However, for compatibility with the HttpClient, it returns a cancellation
                                        //

                                        taskSource.SetCanceled();
                                    }
                                    else
                                    {
                                        HttpResponseMessage msg;
                                        if (deserializationException == null && TryGetHttpResponseMessage(responsePacket, out msg))
                                        {
                                            msg.RequestMessage = request;
                                            taskSource.SetResult(msg);
                                        }
                                        else
                                        {
                                            taskSource.SetException(GetWrappedException("An error occurred while reading the response.", deserializationException));
                                        }
                                    }

                                    lock (localVariableInitLock)
                                    {
                                        callbackHandle.Unregister(null);
                                    }
                                }
                                catch
                                {
                                    //TODO: Log this: 
                                    // the code in the try block should be safe so this catch block should never be called, 
                                    // hoewever, this delegate is called on a separate thread and should be protected.
                                }
                                finally
                                {
                                    CleanupMessagingResources(arrival, receivedEvent);
                                }
                            },
                                null,
                                IsRequestTimeoutInfinite(requestOptions) ? -1 : (long)requestTimeout.TotalMilliseconds,
                                true);

                    }

                    responseArrivalNotification += arrival;
                }

                //Send message
                model.Channel.BasicPublish(exchangeName,
                                messageMapper.GetRoutingKey(request) ?? AmqpUtils.GetWorkQueueRoutingKey(),
                                basicProperties,
                                request.ToHttpRequestPacket().Serialize());

                //Close channel
                CloseAmqpModel(model);
                modelClosed = true;


                if (requestTimeout == TimeSpan.Zero)
                {
                    //TODO: Investigate adding a publisher confirm for zero timeout messages so we know that RabbitMQ did pick up the message before replying OK.

                    //Zero timespan means the client isn't interested in a response
                    taskSource.SetResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[0]) });

                    CleanupMessagingResources(arrival, receivedEvent);
                }

                //TODO: Verify that calls to Wait() on the task do not loop for change and instead rely on Kernel for notification

                return taskSource.Task;

            }
            catch (Exception ex)
            {
                //TODO: Log this

                if (!modelClosed)
                {
                    CloseAmqpModel(model);
                }
                CleanupMessagingResources(arrival, receivedEvent);

                if (ex is HttpRequestException)
                {
                    throw;
                }
                else
                {
                    throw GetWrappedException("An error occurred while sending the request.", ex);
                }

            }


        }


        protected override void Dispose(bool disposing)
        {
            //TODO: Confirm that this does in fact kill all background threads

            disposed = true;

            if (_clientPool != null) _clientPool.Dispose();

            DisposeConnection(conn); // Dispose client connection

            base.Dispose(disposing);

        }

        private void CloseAmqpModel(AmqpModelContainer model)
        {
            if (model != null)
            {
                model.Close();
            }
        }

        private void CleanupMessagingResources(Action<BasicDeliverEventArgs> arrival, ManualResetEventSlim receivedEvent)
        {
            if (arrival != null)
            {
                try
                {
                    //TODO: Investigate if removing/adding delegates is threadsafe.
                    responseArrivalNotification -= arrival;
                }
                catch { }
            }

            if (receivedEvent != null)
            {
                receivedEvent.Dispose();
            }
        }

        private TimeSpan GetRequestTimeout(RequestOptions options)
        {
            return GetTimeoutValue(options).Duration();
        }

        private bool IsRequestTimeoutInfinite(RequestOptions options)
        {
            return GetTimeoutValue(options) == System.Threading.Timeout.InfiniteTimeSpan; //new TimeSpan(0, 0, 0, 0, -1)
        }

        private TimeSpan GetTimeoutValue(RequestOptions options)
        {
            TimeSpan timeoutVal = this.Timeout;

            if (options != null && options.Timeout.HasValue)
            {
                timeoutVal = options.Timeout.Value;
            }
            return timeoutVal;
        }


        private void StartCallbackQueueConsumer()
        {
            //TODO: Double-checked locking -- make this better
            //TODO: Consider moving the conn related checks into a pooler method
            if (callbackConsumer == null || conn == null || !isInConsumerLoop || !conn.IsOpen)
            {
                lock (callbackConsumerStartSync)
                {
                    if (!(callbackConsumer == null || conn == null || !isInConsumerLoop || !conn.IsOpen)) return;

                    //This method waits on this signal to make sure the callbackprocessor thread either started successfully or failed.
                    ManualResetEventSlim consumerSignal = new ManualResetEventSlim(false);
                    Exception consumerSignalException = null;

                    Thread callBackProcessor = new Thread(p =>
                    {
                        IConnection callbackConn = null;
                        AmqpChannelPooler pool = null;
                        ConcurrentQueueingConsumer consumer = null;
                        try
                        {
                            //NOTE: This is the only place where connections are created in the client
                            //NOTE: CreateConnection() can always throw RabbitMQ.Client.Exceptions.BrokerUnreachableException
                            callbackConn = connectionFactory.CreateConnection();

                            //Swap out client connection and pooler, so other threads can use the new objects:

                            //First Swap out old pool with new pool
                            pool = new AmqpChannelPooler(callbackConn);
                            var oldpool = Interlocked.Exchange(ref _clientPool, pool);

                            //then swap out old connection with new one
                            var oldconn = Interlocked.Exchange(ref conn, callbackConn);

                            //Dispose old pool
                            if (oldpool != null)
                            {
                                oldpool.Dispose();
                            }

                            //Dispose old connection
                            DisposeConnection(oldconn);

                            //Start consumer
                            AmqpModelContainer channelContainer = null;
                            try
                            {
                                channelContainer = pool.GetModel(ChannelFlags.Consumer);
                                IModel channel = channelContainer.Channel;

                                //The queue is set to be auto deleted once the last consumer stops using it.
                                //However, RabbitMQ will not delete the queue if no consumer ever got to use it.
                                //Passing x-expires in solves that: It tells RabbitMQ to delete the queue, if no one uses it within the specified time.

                                var callbackQueueArgs = new Dictionary<string, object>();
                                callbackQueueArgs.Add("x-expires", (long)AmqpUtils.GetCallbackQueueExpiry().TotalMilliseconds);

                                //Declare call back queue
                                channel.QueueDeclare(callbackQueueName, false, false, true, callbackQueueArgs);

                                consumer = new ConcurrentQueueingConsumer(channel);

                                //Set consumerCancelled to true on consumer cancellation
                                consumerCancelled = false;
                                consumer.ConsumerCancelled += (s, e) => { consumerCancelled = true; };

                                //Start consumer
                                channel.BasicQos(0, 50, false);
                                channel.BasicConsume(callbackQueueName, false, consumer);

                                //Set callbackConsumer to consumer
                                Interlocked.Exchange(ref callbackConsumer, consumer);

                                //Notify outer thread that channel has started consumption
                                consumerSignal.Set();

                                BasicDeliverEventArgs evt;

                                isInConsumerLoop = true;

                                while (true)
                                {
                                    try
                                    {
                                        evt = DequeueCallbackQueue();
                                    }
                                    catch
                                    {
                                        //TODO: Log this exception except it's ObjectDisposedException
                                        throw;
                                    }

                                    try
                                    {

                                        // TODO: Consider using a concurrent hashtable here, and use the correlation Id as the key to find the  
                                        // delegate value, and then execute the delegate.
                                        // The current use of an event works okay when a small number of requests are waiting for responses silmultaneously
                                        // If the number of requests waiting for responses are in the thousands then things will get slow
                                        // because all delegates are triggered for each incoming response event.

                                        //NOTE: This means correlation id will be passed into CleanUpMessagingResources to find delegate.

                                        //Work on Step 1 above, followed by Step 2 outlined in the TODO in line 230.

                                        var copy = Interlocked.CompareExchange(ref responseArrivalNotification, null, null);
                                        if (copy != null)
                                        {
                                            copy(evt);
                                        }
                                    }
                                    catch
                                    {
                                        //DO nothing
                                    }

                                    //Acknowledge receipt
                                    channel.BasicAck(evt.DeliveryTag, false);

                                    //Exit loop if consumer is cancelled.
                                    if (consumerCancelled)
                                    {
                                        break;
                                    }
                                }

                            }
                            finally
                            {
                                isInConsumerLoop = false;

                                if (channelContainer != null)
                                {
                                    if (consumer != null && !consumerCancelled)
                                    {
                                        try
                                        {
                                            channelContainer.Channel.BasicCancel(consumer.ConsumerTag);
                                        }
                                        catch { }
                                    }

                                    channelContainer.Close();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            //TODO: Log error (Except it's object disposed exception)

                            //Set Exception object which will be throw by signal waiter
                            Interlocked.Exchange(ref consumerSignalException, ex);

                            //Notify outer thread to move on, in case it's still waiting
                            try
                            {
                                consumerSignal.Set();
                            }
                            catch { }


                        }
                        finally
                        {
                            if (pool != null)
                            {
                                pool.Dispose();
                            }
                            DisposeConnection(callbackConn);
                        }

                    });

                    //Start Thread
                    callBackProcessor.Name = "RestBus RabbitMQ Client Callback Queue Consumer";
                    callBackProcessor.IsBackground = true;
                    callBackProcessor.Start();

                    //Wait for Thread to start consuming messages
                    consumerSignal.Wait();
                    consumerSignal.Dispose();

                    //Examine exception if it were set and rethrow it
                    if (consumerSignalException != null)
                    {
                        throw consumerSignalException;
                    }

                    //No more code from this point in this method

                }
            }

        }

        private void EnsureNotStartedOrDisposed()
        {
            if (disposed) throw new ObjectDisposedException(GetType().FullName);
            if (hasKickStarted) throw new InvalidOperationException("This instance has already started one or more requests. Properties can only be modified before sending the first request.");
        }

        private void PrepareMessage(HttpRequestMessage request)
        {
            //Combine RequestUri with BaseRequest
            if (request.RequestUri == null)
            {
                request.RequestUri = this.BaseAddress;
            }
            else if (!request.RequestUri.IsAbsoluteUri)
            {
                if (this.BaseAddress != null)
                {
                    request.RequestUri = new Uri(this.BaseAddress, request.RequestUri);
                }
            }

            //Append default request headers
            if (this.DefaultRequestHeaders != null)
            {
                foreach (var header in this.DefaultRequestHeaders)
                {
                    if (!request.Headers.Contains(header.Key))
                    {
                        request.Headers.Add(header.Key, header.Value);
                    }
                }
            }

        }

        private BasicDeliverEventArgs DequeueCallbackQueue()
        {
            while (true)
            {
                if (disposed) throw new ObjectDisposedException("Client has been disposed");

                BasicDeliverEventArgs item;
                if (callbackConsumer.TryInstantDequeue(out item))
                {
                    return item;
                }

                Thread.Sleep(1);
            }
        }

        private static HttpRequestException GetWrappedException(string message, Exception innerException)
        {
            return new HttpRequestException(message, innerException);
        }

        private static void DisposeConnection(IConnection connection)
        {
            if (connection != null)
            {

                try
                {
                    connection.Close();
                }
                catch
                {
                    //TODO: Log Error
                }

                try
                {
                    connection.Dispose();
                }
                catch
                {
                    //TODO: Log Error
                }
            }
        }

        private static bool TryGetHttpResponseMessage(HttpResponsePacket packet, out HttpResponseMessage response)
        {
            try
            {
                response = new HttpResponseMessage
                {
                    Content = new ByteArrayContent(packet.Content ?? new byte[0]),
                    Version = new Version(packet.Version),
                    ReasonPhrase = packet.StatusDescription,
                    StatusCode = (System.Net.HttpStatusCode)packet.StatusCode
                };

                packet.PopulateHeaders(response.Content.Headers, response.Headers);
            }
            catch
            {
                response = null;
                return false;
            }

            return true;
        }

        private static RabbitMQMessagingProperties GetRequestOptionsMessagingProperties(RequestOptions options)
        {
            return (options.Properties as RabbitMQMessagingProperties) ?? _defaultMessagingProperties;
        }
    }



}
