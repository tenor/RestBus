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
using System.Collections.Concurrent;
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
        readonly ExchangeConfiguration exchangeConfig;
        readonly string clientId;
        readonly string exchangeName;
        readonly string indirectReplyToQueueName;
        volatile string callbackQueueName;
        readonly ConnectionFactory connectionFactory;
        volatile ConcurrentQueueingConsumer callbackConsumer;
        readonly ManualResetEventSlim responseQueued = new ManualResetEventSlim();
        IConnection conn;
        volatile AmqpChannelPooler _clientPool;
        volatile bool isInConsumerLoop;
        volatile bool consumerCancelled;
        volatile bool reconnectToServer;
        volatile bool seenRequestExpectingResponse;
        readonly ConcurrentDictionary<string, ExpectedResponse> expectedResponses;

        readonly object reconnectionSync = new object();
        object exchangeDeclareSync = new object();
        volatile int lastExchangeDeclareTickCount = 0;
        volatile bool disposed = false;
        readonly CancellationTokenSource disposedCancellationSource = new CancellationTokenSource();

        volatile bool hasKickStarted = false;
        private Uri baseAddress;
        private HttpRequestHeaders defaultRequestHeaders;
        private TimeSpan timeout;

        internal const int HEART_BEAT = 30;
        const string DIRECT_REPLY_TO_QUEUENAME_ARG = "amq.rabbitmq.reply-to";
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
            this.exchangeConfig = messageMapper.GetExchangeConfig();
            if (exchangeConfig == null) throw new ArgumentException("messageMapper.GetExchangeConfig() returned null");

            this.clientId = AmqpUtils.GetNewExclusiveQueueId();
            //TODO: Get ExchangeKind from CLient.Settings.ExchangeKind
            this.exchangeName = AmqpUtils.GetExchangeName(exchangeConfig, ExchangeKind.Direct);
            this.indirectReplyToQueueName = AmqpUtils.GetCallbackQueueName(exchangeConfig, clientId);

            //Map request to RabbitMQ Host and exchange, 
            this.connectionFactory = new ConnectionFactory();
            connectionFactory.Uri = exchangeConfig.ServerUris[0].Uri;
            connectionFactory.RequestedHeartbeat = HEART_BEAT;

            //Set ClientSettings
            this.Settings = new ClientSettings(this); // Always have a default version if it wasn't passed in.

            //Initialize expectedResponses
            expectedResponses = new ConcurrentDictionary<string, ExpectedResponse>();
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
                if (value != System.Threading.Timeout.InfiniteTimeSpan  && value < TimeSpan.Zero)
                {
                    throw new ArgumentOutOfRangeException("value");
                }
                EnsureNotStartedOrDisposed();
                timeout = value;
            }
        }

        public ClientSettings Settings { get; }

        /// <summary>Cancel all pending requests on this instance.</summary>
        public void CancelPendingRequests()
        {
            throw new NotImplementedException();
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
            var messageProperties = GetMessagingProperties(requestOptions);

            //Declare messaging resources
            ExpectedResponse arrival = null;
            AmqpModelContainer model = null;
            bool modelClosed = false;
            string correlationId = null;

            try
            {
                TimeSpan requestTimeout = GetRequestTimeout(requestOptions);
                if (requestTimeout != TimeSpan.Zero) seenRequestExpectingResponse = true;

                if (seenRequestExpectingResponse)
                {
                    //This client has seen a request expecting a response so
                    //Start Callback consumer if it hasn't started
                    StartCallbackQueueConsumer();
                }
                else
                {
                    //Client has never seen any request expecting a response
                    //so just try to connect if not already connected
                    ConnectToServer();
                }

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

                RedeclareExchangesAndQueues(model);

                BasicProperties basicProperties = new BasicProperties();

                //Set message delivery mode -- Make message persistent if either:
                // 1. Properties.Persistent is true
                // 2. exchangeConfig.PersistentMessages is true and Properties.Persistent is null
                // 3. exchangeConfig.PersistentMessages is true and Properties.Persistent is true
                if (messageProperties.Persistent == true || (exchangeConfig.PersistentMessages && messageProperties.Persistent != false))
                {
                    basicProperties.Persistent = true;
                }

                //Set Exchange Headers
                var exchangeHeaders = messageProperties.Headers ?? messageMapper.GetHeaders(request);
                if (exchangeHeaders != null)
                {
                    basicProperties.Headers = exchangeHeaders;
                }

                if (requestTimeout != TimeSpan.Zero)
                {
                    //Set Reply to queue
                    basicProperties.ReplyTo = callbackQueueName;

                    //Set CorrelationId
                    correlationId = correlationIdGen.GetNextId();
                    basicProperties.CorrelationId = correlationId;

                    //Set Expiration if messageProperties doesn't override Client.Timeout, RequestOptions and MessageMapper.
                    if (!messageProperties.Expiration.HasValue && requestTimeout != System.Threading.Timeout.InfiniteTimeSpan 
                        && ( exchangeConfig.MessageExpires == null || exchangeConfig.MessageExpires(request)))
                    {
                        if (requestTimeout.TotalMilliseconds > Int32.MaxValue)
                        {
                            basicProperties.Expiration = Int32.MaxValue.ToString();
                        }
                        else
                        {
                            basicProperties.Expiration = ((int)requestTimeout.TotalMilliseconds).ToString();
                        }
                    }


                    //TODO: Better to just check if cancellationHasbeen requested instead of checking if it's None
                    if (!cancellationToken.Equals(System.Threading.CancellationToken.None))
                    {
                        //TODO: Have cancellationtokens cancel event trigger callbackHandle
                        //In fact turn this whole thing into an extension
                    }

                    //Initialize response arrival object
                    arrival = new ExpectedResponse();


                    //Spawning a new task to wait on the MRESlim is slower than using ThreadPool.RegisterWaitForSingleObject
                    //
                    //TODO: Test task vs RegisterWaitForSingleObject modes in a super fast network environment with 40, 100, 200 all the way to 1000 threads to see what method has fastest throughput.

#if WAIT_FOR_RESPONSE_IN_TASK_MODE

                    //Wait for response arrival event on a new task

                    Task.Factory.StartNew(() =>
                    {
                        bool succeeded = arrival.ReceivedEvent.Wait(
                            requestTimeout.TotalMilliseconds > Int32.MaxValue ? TimeSpan.FromMilliseconds(Int32.MaxValue) : requestTimeout /* Covers InfiniteTimeSpan */,
                            cancellationToken);

                        Thread.MemoryBarrier(); //Ensure non-cached versions of arrival are read

                        //It failed either because it timed out or was cancelled
                        //HttpClient treats both scenarios the same way.

                        try
                        {
                            SetResponseResult(request, !succeeded, arrival, taskSource);
                        }
                        catch
                        {
                            //TODO: Log this: 
                            // the code in the try block should be safe so this catch block should never be called, 
                            // hoewever, this delegate is called on a separate thread and should be protected.
                        }
                        finally
                        {
                            CleanupMessagingResources(correlationId, arrival);
                        }

                    }, cancellationToken);

#else

                    //Wait for response arrival event on the ThreadPool:

                    var localVariableInitLock = new object();

                    lock (localVariableInitLock)
                    {
                        //TODO: Have cancellationToken signal WaitHandle so that threadpool stops waiting.

                        RegisteredWaitHandle callbackHandle = null;
                        callbackHandle = ThreadPool.RegisterWaitForSingleObject(arrival.ReceivedEvent.WaitHandle,
                            (state, timedOut) =>
                            {
                                //TODO: Investigate, this memorybarrier might be unnecessary since the thread is released from the threadpool
                                //after deserializationException and responsePacket is set.
                                Thread.MemoryBarrier(); //Ensure non-cached versions of arrival are read
                                try
                                {
                                    //TODO: Check Cancelation Token when it's implemented

                                    SetResponseResult(request, timedOut, arrival, taskSource);

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
                                    CleanupMessagingResources(correlationId, arrival);
                                }
                            },
                                null,
                                requestTimeout == System.Threading.Timeout.InfiniteTimeSpan ? System.Threading.Timeout.Infinite : (long)requestTimeout.TotalMilliseconds,
                                true);

                    }
#endif

                    expectedResponses[correlationId] = arrival;
                }
                else if (!messageProperties.Expiration.HasValue && (exchangeConfig.MessageExpires == null || exchangeConfig.MessageExpires(request)))
                {
                    //Request has a zero timeout and the message mapper indicates it should expire and messageproperties expiration is not set:
                    //Set the expiration to zero which means RabbitMQ will only transmit if there is a consumer ready to receive it.
                    //If there is no ready consumer, RabbitMQ drops the message. See https://www.rabbitmq.com/ttl.html

                    basicProperties.Expiration = "0";
                }

                //Set expiration if set in message properties
                if (messageProperties.Expiration.HasValue)
                {
                    if (messageProperties.Expiration != System.Threading.Timeout.InfiniteTimeSpan)
                    {
                        var expiration = messageProperties.Expiration.Value.Duration();
                        if (expiration.TotalMilliseconds > Int32.MaxValue)
                        {
                            basicProperties.Expiration = Int32.MaxValue.ToString();
                        }
                        else
                        {
                            basicProperties.Expiration = ((int)expiration.TotalMilliseconds).ToString();
                        }
                    }
                    else
                    {
                        //Infinite Timespan indicates that message should never expire
                        basicProperties.ClearExpiration();
                    }
                }

                //TODO: Implement routing to a different exchangeKind via substituting exchangeName
                //Send message
                model.Channel.BasicPublish(exchangeName,
                                messageProperties.RoutingKey ?? messageMapper.GetRoutingKey(request) ?? AmqpUtils.GetWorkQueueRoutingKey(),
                                basicProperties,
                                request.ToHttpRequestPacket().Serialize());

                //Close channel
                CloseAmqpModel(model);
                modelClosed = true;


                if (requestTimeout == TimeSpan.Zero)
                {
                    //TODO: Investigate adding a publisher confirm for zero timeout messages so we know that RabbitMQ did pick up the message before replying OK.

                    //Zero timespan means the client isn't interested in a response
                    //TODO: Have new ByteArrayContent be a static object.
                    taskSource.SetResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[0]) });

                    CleanupMessagingResources(correlationId, arrival);
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
                CleanupMessagingResources(correlationId, arrival);

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

        internal void EnsureNotStartedOrDisposed()
        {
            if (disposed) throw new ObjectDisposedException(GetType().FullName);
            if (hasKickStarted) throw new InvalidOperationException("This instance has already started one or more requests. Properties can only be modified before sending the first request.");
        }

        protected override void Dispose(bool disposing)
        {
            //TODO: Confirm that this does in fact kill all background threads

            disposed = true;
            disposedCancellationSource.Cancel();

            if (_clientPool != null) _clientPool.Dispose();

            DisposeConnection(conn); // Dispose client connection

            base.Dispose(disposing);
            responseQueued.Dispose();
            disposedCancellationSource.Dispose();

        }

        private void RedeclareExchangesAndQueues(AmqpModelContainer model)
        {
            //Redeclare exchanges and queues every minute if exchanges and queues are transient, or the first time client is sending a message
            TimeSpan elapsedSinceLastDeclareExchange = TimeSpan.FromMilliseconds(Environment.TickCount - lastExchangeDeclareTickCount);

            //Discovering firstDeclare by comparing lastExchangeDeclareTickCount to zero is not perfect 
            //because tickcount can wrap back to zero (through the negative number range), if client is running long enough.
            //However, redeclaring exchanges and queues are a safe operation, so this is okay if it occurs more than once in persistent queues.
            bool firstDeclare = lastExchangeDeclareTickCount == 0;
            if (firstDeclare || (!exchangeConfig.PersistentWorkQueuesAndExchanges && (elapsedSinceLastDeclareExchange.TotalMilliseconds < 0 || elapsedSinceLastDeclareExchange.TotalSeconds > 60)))
            {
                if (!firstDeclare)
                {
                    //All threads must attempt to declare exchanges and queues if it hasn't been previously declared 
                    //(for instance, all threads were started at once)
                    //So do not swap out this value on first declare
                    lastExchangeDeclareTickCount = Environment.TickCount;
                }
                AmqpUtils.DeclareExchangeAndQueues(model.Channel, exchangeConfig, exchangeDeclareSync, null);
                if (firstDeclare)
                {
                    //Swap out this value after declaring on firstdeclare
                    lastExchangeDeclareTickCount = Environment.TickCount;
                }
            }
        }

        private void CloseAmqpModel(AmqpModelContainer model)
        {
            if (model != null)
            {
                model.Close();
            }
        }

        private void CleanupMessagingResources(string correlationId, ExpectedResponse expectedResponse)
        {
            if (!String.IsNullOrEmpty(correlationId))
            {
                ExpectedResponse unused;
                expectedResponses.TryRemove(correlationId, out unused);
            }

            if (expectedResponse != null && expectedResponse.ReceivedEvent != null)
            {
                expectedResponse.ReceivedEvent.Dispose();
            }
        }

        private TimeSpan GetRequestTimeout(RequestOptions options)
        {
            TimeSpan timeoutVal = this.Timeout;

            if (options != null && options.Timeout.HasValue)
            {
                timeoutVal = options.Timeout.Value;
            }

            return timeoutVal.Duration();
        }

        // TODO: Consider introducing RPC channels into the channel pool system.
        // RPC Channels will have associated Consumers. If no consumer is used then a new one is created just before the channel is used.
        // The RPC Channel will publish and consume messages on it's own queue.
        // It elimnates the DiscoverDirectReplyToQueueName method here which is sort of a hack.
        // It may also eliminate the StartCallbackQueueConsumer for the most part.
        // There may be some speed improvements since each consuer can receive their callbacks at the same time unlike in the queued system.
        //
        // There are two ways this can be done:
        // 1. Have a shared queue and have a dedicated thread that polls the queue and calls related events  in the dictionary (quite similar to the status quo)
        // 2. Implement a new event based Consumer (or use the one already in the Rabbit code base), the event handler will check if a flag has been set in the channel that says
        // owner is expecting a response. If that flag is set, then a ManualResetEventSlim is set. and the I/O completion port exits, if that flag was not set then the I/O completion port just exits.
        // The ManualResetEventSlim wakes up a thread on the thread pool to handle the rest of the processing.
        // The latter methods seems more beneficial since you'll no longer need a dedicated thread polling the worker pool, you'd have to find a way to handle disconnections and creating new pools
        // since StartCallbackQueueConsumer handles that now.
        //
        // It may be prudent for the pool to check Model.IsOpen before handing over an RPC channel to a caller.
        // 
        // The current StartCallbackQueueConsumer will then be refactored into an ICallback/IResponse/IReplyStrategy interface
        // Users who don't want the direct reply-to feature (or who use older version of RabbitMQ) will use the old StartCallbackQueueConsumer (which will be stripped off Direct Reply-To code paths)
        // Users who opt into Direct Reply To (and have v3.4.0 or later) will use the new IStrategy.
        // strategy interface.
        //
        // NOTE: There'll still be "Consumer" channels used by the Subscriber and the old strategy, maybe change the name to "Server/Servlet" channels or something.
        private void StartCallbackQueueConsumer()
        {
            //TODO: Double-checked locking -- make this better
            //TODO: Consider moving the conn related checks into a pooler method
            if (callbackConsumer == null || conn == null || !isInConsumerLoop || !conn.IsOpen)
            {
                //NOTE: Same lock is used in StartCallbackConsumer
                lock (reconnectionSync)
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
                            //Do not create a new connection or pool if there is a good one already existing (possibly created by ConnectToServer()
                            //unless consumer explicitly signalled to reconnect to server
                            if (conn == null || !conn.IsOpen || reconnectToServer)
                            {
                                CreateNewConnectionAndChannelPool(out callbackConn, out pool);
                            }

                            //Start consumer
                            AmqpModelContainer channelContainer = null;
                            try
                            {
                                channelContainer = pool.GetModel(ChannelFlags.Consumer);
                                IModel channel = channelContainer.Channel;

                                if (Settings.DisableDirectReplies || !channelContainer.IsDirectReplyToCapable)
                                {
                                    DeclareIndirectReplyToQueue(channel, indirectReplyToQueueName);
                                }

                                consumer = new ConcurrentQueueingConsumer(channel, responseQueued);

                                //Set consumerCancelled to true on consumer cancellation
                                consumerCancelled = false;
                                consumer.ConsumerCancelled += (s, e) => { consumerCancelled = true; };

                                channel.BasicQos(0, 50, false);
                                //Start consumer:

                                string replyToQueueName;

                                if (Settings.DisableDirectReplies || !channelContainer.IsDirectReplyToCapable)
                                {
                                    channel.BasicConsume(indirectReplyToQueueName, Settings.AckBehavior == ClientAckBehavior.Automatic, consumer);
                                    replyToQueueName = indirectReplyToQueueName;
                                }
                                else
                                {
                                    channel.BasicConsume(DIRECT_REPLY_TO_QUEUENAME_ARG, true, consumer);

                                    //Discover direct reply to queue name
                                    replyToQueueName = DiscoverDirectReplyToQueueName(channel, indirectReplyToQueueName);
                                }

                                //Set callbackConsumer to consumer
                                callbackQueueName = replyToQueueName;
                                callbackConsumer = consumer;

                                //Notify outer thread that channel has started consumption
                                consumerSignal.Set();

                                BasicDeliverEventArgs evt;
                                ExpectedResponse expected;
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

                                    expected = null;
                                    if (!String.IsNullOrEmpty(evt.BasicProperties.CorrelationId) && expectedResponses.TryRemove(evt.BasicProperties.CorrelationId, out expected))
                                    {
                                        try
                                        {
                                            expected.Response = HttpResponsePacket.Deserialize(evt.Body);
                                        }
                                        catch (Exception ex)
                                        {
                                            expected.DeserializationException = ex;
                                        }

                                        //NOTE: The ManualResetEventSlim.Set() method can be called after the object has been disposed
                                        //So no worries about the Timeout disposing the object before the response comes in.
                                        expected.ReceivedEvent.Set();
                                    }

                                    //Acknowledge receipt:
                                    //In ClientBehavior.Automatic mode
                                    //Client acks all received messages, even if it wasn't the expected one or even if it wasn't expecting anything.
                                    //This prevents a situation where crap messages are sent to the client but the good expected message is stuck behind the
                                    //crap ones and isn't delivered because the crap ones in front of the queue aren't acked and crap messages exceed prefetchCount.

                                    //In ClientAckBehavior.ValidResponses mode (and Direct Reply to is not in effect):
                                    //Client only acks expected messages if they could be deserialized
                                    //If not, they are rejected.

                                    if ((Settings.DisableDirectReplies || !channelContainer.IsDirectReplyToCapable) && Settings.AckBehavior == ClientAckBehavior.ValidResponses)
                                    {
                                        if (expected != null && expected.DeserializationException != null)
                                        {
                                            channel.BasicAck(evt.DeliveryTag, false);
                                        }
                                        else
                                        {
                                            channel.BasicReject(evt.DeliveryTag, false);
                                        }
                                    }

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
                                reconnectToServer = true;

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
                            consumerSignalException = ex;

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
                    Thread.MemoryBarrier(); //Ensure we have the non-cached version of consumerSignalException
                    if (consumerSignalException != null)
                    {
                        throw consumerSignalException;
                    }

                    //No more code from this point in this method

                }
            }

        }

        private void ConnectToServer()
        {
            if (conn == null || !conn.IsOpen)
            {
                //TODO: Can double-checked locking here be simplified?

                //NOTE: Same lock is used in StartCallbackConsumer
                lock(reconnectionSync)
                {
                    if (conn == null || !conn.IsOpen)
                    {
                        IConnection newConn;
                        AmqpChannelPooler pool;
                        CreateNewConnectionAndChannelPool(out newConn, out pool);
                    }
                }
            }
        }

        private void CreateNewConnectionAndChannelPool(out IConnection newConn, out AmqpChannelPooler pool)
        {
            //NOTE: This is the only place where connections are created in the client
            //NOTE: CreateConnection() can always throw RabbitMQ.Client.Exceptions.BrokerUnreachableException
            newConn = connectionFactory.CreateConnection();

            //Swap out client connection and pooler, so other threads can use the new objects:

            //First Swap out old pool with new pool
            pool = new AmqpChannelPooler(newConn);
            var oldpool = Interlocked.Exchange(ref _clientPool, pool);

            //then swap out old connection with new one
            var oldconn = Interlocked.Exchange(ref conn, newConn);

            //Dispose old pool
            if (oldpool != null)
            {
                oldpool.Dispose();
            }

            //Dispose old connection
            DisposeConnection(oldconn);
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

                responseQueued.Wait(disposedCancellationSource.Token);
                responseQueued.Reset();
            }
        }

        private static void DeclareIndirectReplyToQueue(IModel channel, string queueName)
        {
            //The queue is set to be auto deleted once the last consumer stops using it.
            //However, RabbitMQ will not delete the queue if no consumer ever got to use it.
            //Passing x-expires in solves that: It tells RabbitMQ to delete the queue, if no one uses it within the specified time.

            var callbackQueueArgs = new Dictionary<string, object>();
            callbackQueueArgs.Add("x-expires", (long)AmqpUtils.GetCallbackQueueExpiry().TotalMilliseconds);

            //TODO: AckBehavior is applied here.

            //Declare call back queue
            channel.QueueDeclare(queueName, false, false, true, callbackQueueArgs);
        }

        /// <summary>
        /// Discovers the Direct reply-to queue name ( https://www.rabbitmq.com/direct-reply-to.html ) by messaging itself.
        /// </summary>
        private static string DiscoverDirectReplyToQueueName(IModel channel, string indirectReplyToQueueName)
        {
            DeclareIndirectReplyToQueue(channel, indirectReplyToQueueName);

            var receiver = new ConcurrentQueueingConsumer(channel);
            var receiverTag = channel.BasicConsume(indirectReplyToQueueName, true, receiver);

            channel.BasicPublish(String.Empty, indirectReplyToQueueName, true, new BasicProperties { ReplyTo = DIRECT_REPLY_TO_QUEUENAME_ARG }, new byte[0]);

            BasicDeliverEventArgs delivery;
            using (ManualResetEventSlim messageReturned = new ManualResetEventSlim())
            {
                EventHandler<BasicReturnEventArgs> returnHandler = null;
                Interlocked.Exchange(ref returnHandler, (a, e) => { messageReturned.Set(); try { receiver.Model.BasicReturn -= returnHandler; } catch { } });
                receiver.Model.BasicReturn += returnHandler;

                System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
                watch.Start();
                while (!receiver.TryInstantDequeue(out delivery))
                {
                    Thread.Sleep(1);
                    if (watch.Elapsed > TimeSpan.FromSeconds(10) || messageReturned.IsSet)
                    {
                        break;
                    }
                }
                watch.Stop();

                if (!messageReturned.IsSet)
                {
                    try
                    {
                        receiver.Model.BasicReturn -= returnHandler;
                    }
                    catch { }
                }

                try
                {
                    channel.BasicCancel(receiverTag);
                }
                catch { }
            }

            if (delivery == null)
            {
                throw new InvalidOperationException("Unable to determine direct reply-to queue name.");
            }

            var result = delivery.BasicProperties.ReplyTo;
            if (result == null || result == DIRECT_REPLY_TO_QUEUENAME_ARG || !result.StartsWith(DIRECT_REPLY_TO_QUEUENAME_ARG))
            {
                throw new InvalidOperationException("Discovered direct reply-to queue name (" + (result ?? "null") + ") was not in expected format.");
            }

            return result;
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

        private static void SetResponseResult(HttpRequestMessage request, bool timedOut, ExpectedResponse arrival, TaskCompletionSource<HttpResponseMessage> taskSource)
        {
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
                if (arrival.DeserializationException == null)
                {
                    var res = arrival.Response;
                    if (res != null)
                    {
                        //Add/Update Content-Length Header
                        //TODO: Is there any need to add this here if it's subsequently removed/updated by TryGetHttpResponseMessage/HttpPacket.PopulateHeaders? (Is this useful in the exception/other path scenario?
                        res.Headers["Content-Length"] = new string[] { (res.Content == null ? 0 : res.Content.Length).ToString() }; ;
                    }
                    else
                    {
                        //TODO: Log this -- Critical issue (or just assert)
                    }
                }

                HttpResponseMessage msg;
                if (arrival.DeserializationException == null && TryGetHttpResponseMessage(arrival.Response, out msg))
                {
                    msg.RequestMessage = request;
                    taskSource.SetResult(msg);
                }
                else
                {
                    taskSource.SetException(GetWrappedException("An error occurred while reading the response.", arrival.DeserializationException));
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

        private static RabbitMQMessagingProperties GetMessagingProperties(RequestOptions options)
        {
            if (options == null) return _defaultMessagingProperties;
            return (options.Properties as RabbitMQMessagingProperties) ?? _defaultMessagingProperties;
        }
    }



}
