using RabbitMQ.Client;
using RabbitMQ.Client.Framing.v0_9_1;
using RestBus.RabbitMQ.Common;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace RestBus.RabbitMQ.Client
{

    //TODO: Flesh out class, make the incompatible methods such as the StreamXXX ones work somehow or throw a NotSUpportedException

    //TODO: RestBusClient should derive directly from HttpMessageInvoker
    //Problem with HTTPClient include
    //1. TimeOut cannot be controlled (Needed to introduce WaitForResponse which can be eliminated if derived directly )
    //2. CancelPendingRequest cannot be overriden
    //3. The BIG one is SendAsync overloads that cannot be overriden but call into the System.Net.Http stack
    //4. IsStarted can also be checked to enforce things like Timeout, BaseUrl and DefaultHeaders

    //The drawback to this approach is that all the regular Get/Post/Async methods will have to be reimplemented + all fields and properties
    //Also extension methods would have to be called either against RestBus or HttpMessageInvoker

    public class RestBusClient : HttpClient
    {
        const string REQUEST_OPTIONS_KEY = "_rb_options";

        readonly IMessageMapper messageMapper;
        readonly ExchangeInfo exchangeInfo;
        readonly string clientId;
        readonly string exchangeName;
        readonly string callbackQueueName;
        readonly ConnectionFactory connectionFactory;
        QueueingBasicConsumer callbackConsumer = null;
        IConnection conn = null;
        event Action<global::RabbitMQ.Client.Events.BasicDeliverEventArgs> responseArrivalNotification = null;

        readonly object callbackConsumerStartSync = new object();
        object exchangeDeclareSync = new object();
        int lastExchangeDeclareTickCount = 0;
        volatile bool disposed = false;

        public const int HEART_BEAT = 30;

        public RestBusClient(IMessageMapper messageMapper) : base()
        {
            this.messageMapper = messageMapper;
            this.exchangeInfo = messageMapper.GetExchangeInfo();
            this.clientId = Utils.GetRandomId();
            this.exchangeName = Utils.GetExchangeName(exchangeInfo);
            this.callbackQueueName = Utils.GetCallbackQueueName(exchangeInfo, clientId);
            this.WaitForResponse = true;

            //Map request to RabbitMQ Host and exchange, 
            this.connectionFactory = new ConnectionFactory();
            connectionFactory.Uri = exchangeInfo.ServerAddress;
            connectionFactory.RequestedHeartbeat = HEART_BEAT;
        }

        //TODO: FIgure out how to handle the CancelPendingRequests() method (It's not overridable)

        //TODO: Confirm that this method is thread safe
        public override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException("request");

            if (request.RequestUri == null && BaseAddress == null )
            {
               throw new InvalidOperationException("The request URI must either be set or BaseAddress must be set");
            }

            if (disposed) throw new ObjectDisposedException("Client has been disposed");

            PrepareMessage(request);

            //Get Request Options
            RequestOptions requestOptions = GetRequestOptions(request);

            //Declare messaging resources
            Action<global::RabbitMQ.Client.Events.BasicDeliverEventArgs> arrival = null;
            ManualResetEventSlim receivedEvent = null;
            IModel channel = null;

            try
            {
                //Start Callback consumer if it hasn't started
                StartCallbackQueueConsumer();

                //TODO: test if conn is null, then leave
                if (conn == null)
                {
                    //TODO: This means a connection could not be created most likely because the server was Unreachable
                    //TODO: Throw some kind of HTTP 500 (Unreachable) exception
                    throw new ApplicationException("This is Bad");
                }

                //NOTE: Do not share channels across threads.
                //TODO: Investigate Channel pooling to see if there is significant increase in throughput on connections with reasonable latency
                channel = conn.CreateModel();

                TimeSpan elapsedSinceLastDeclareExchange = TimeSpan.FromMilliseconds(Environment.TickCount - lastExchangeDeclareTickCount);
                if (lastExchangeDeclareTickCount == 0 || elapsedSinceLastDeclareExchange.TotalMilliseconds < 0 || elapsedSinceLastDeclareExchange.TotalSeconds > 30)
                {
                    //Redeclare exchanges and queues every 30 seconds

                    Interlocked.Exchange(ref lastExchangeDeclareTickCount, Environment.TickCount);
                    Utils.DeclareExchangeAndQueues(channel, exchangeInfo, exchangeDeclareSync, null);
                }


                //TODO: if exchangeInfo wants a Session/Server/Sticky Queue

                string correlationId = Utils.GetRandomId();
                BasicProperties basicProperties = new BasicProperties { CorrelationId = correlationId };

                //TODO: Check if cancellation token was set before operation even began

                var taskSource = new TaskCompletionSource<HttpResponseMessage>();

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
                    bool deserializationError = false;
                    receivedEvent = new ManualResetEventSlim(false);

                    arrival = a =>
                    {
                        if (a.BasicProperties.CorrelationId == correlationId)
                        {
                            //TODO: If deserialization failed then set exception
                            HttpResponsePacket res = null;
                            try
                            {
                                res = HttpResponsePacket.Deserialize(a.Body);
                            }
                            catch
                            {
                                deserializationError = true;
                            }

                            //Add/Update Content-Length Header
                            res.Headers["Content-Length"] = new string[]{(res.Content == null ? 0 : res.Content.Length).ToString()};;

                            if (!deserializationError)
                            {
                                responsePacket = res;
                            }
                            receivedEvent.Set();
                            responseArrivalNotification -= arrival;
                        }
                    };

                    if (!cancellationToken.Equals(System.Threading.CancellationToken.None))
                    {
                        //TODO: Have cancellationtokens cancel event trigger callbackHandle
                        //In fact turn this whole thing into an extension
                    }


                    //Create task for message arrival event
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
                                        //TODO: This should be a HTTP timed out exception;
                                        taskSource.SetException(new ApplicationException());
                                    }
                                    else
                                    {

                                        //TODO: How do we ensure that response (and deserializationError) is properly seen across different threads
                                        HttpResponseMessage msg;
                                        if (!deserializationError && responsePacket.TryGetHttpResponseMessage(out msg))
                                        {
                                            msg.RequestMessage = request;
                                            taskSource.SetResult(msg);
                                        }
                                        else
                                        {
                                            //TODO: This should be one that translates to a bad response message error 
                                            taskSource.SetException(new ApplicationException());
                                        }

                                    }

                                    lock (localVariableInitLock)
                                    {
                                        callbackHandle.Unregister(null);
                                    }
                                }
                                finally
                                {
                                    CleanupMessagingResources(channel, arrival, receivedEvent);
                                }
                            },
                                null,
                                IsRequestTimeoutInfinite(requestOptions) ? -1 : (long)requestTimeout.TotalMilliseconds,
                                true);

                    }

                    responseArrivalNotification += arrival;
                }

                //Send message
                channel.BasicPublish(exchangeName,
                                messageMapper.GetRoutingKey(request) ?? Utils.GetWorkQueueRoutingKey(),
                                basicProperties,
                                (new HttpRequestPacket(request)).Serialize());


                if (requestTimeout == TimeSpan.Zero)
                {
                    //TODO: Investigate adding a publisher confirm for zero timeout messages so we know that RabbitMQ did pick up the message before relying OK.

                    //Zero timespan means the client isn't interested in a response
                    taskSource.SetResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[0]) });

                    CleanupMessagingResources(channel, arrival, receivedEvent);
                }

                //TODO: Verify that calls to Wait() on the task do not loop for change and instead rely on Kernel for notification

                return taskSource.Task;

            }
            catch
            {
                //TODO: Log this

                CleanupMessagingResources(channel, arrival, receivedEvent);

                throw;

            }


        }

        internal static RequestOptions GetRequestOptions(HttpRequestMessage request)
        {
            object reqObj;
            if (request.Properties.TryGetValue(REQUEST_OPTIONS_KEY, out reqObj))
            {
                return reqObj as RequestOptions;
            }

            return null;
        }

        internal static void SetRequestOptions(HttpRequestMessage request, RequestOptions options)
        {
            request.Properties[REQUEST_OPTIONS_KEY] = options;
        }

        private void CleanupMessagingResources(IModel channel, Action<global::RabbitMQ.Client.Events.BasicDeliverEventArgs> arrival, ManualResetEventSlim receivedEvent)
        {
            if (channel != null)
            {
                try
                {
                    channel.Dispose();
                }
                catch { }
            }

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

        //TODO: Get a better name (SkipResponse maybe ??)
        public virtual bool WaitForResponse
        {
            get;
            set;
        }

        private TimeSpan GetRequestTimeout(RequestOptions options)
        {
            return GetTimeoutValue(options).Duration();
        }

        private bool IsRequestTimeoutInfinite(RequestOptions options)
        {
            return GetTimeoutValue(options) == System.Threading.Timeout.InfiniteTimeSpan;
        }

        private TimeSpan GetTimeoutValue(RequestOptions options)
        {
            TimeSpan timeout;
            if (!WaitForResponse)
            {
                timeout = TimeSpan.Zero;
            }
            else
            {
                timeout = this.Timeout;
            }

            if (options != null && options.Timeout.HasValue)
            {
                timeout = options.Timeout.Value;
            }
            return timeout;
        }

        private void StartCallbackQueueConsumer()
        {
            //TODO: DOuble-checked locking -- make this better
            if (callbackConsumer == null || conn == null || !callbackConsumer.IsRunning || !conn.IsOpen)
            {
                lock (callbackConsumerStartSync)
                {
                    if (!(callbackConsumer == null || conn == null || !callbackConsumer.IsRunning || !conn.IsOpen)) return;

                    //This method waits on this signal to make sure the callbackprocessor thread either started successfully or failed.
                    ManualResetEventSlim consumerSignal = new ManualResetEventSlim(false);

                    Thread callBackProcessor = new Thread(p =>
                    {

                        try
                        {

                            //NOTE: CreateConnection() can always throw RabbitMQ.Client.Exceptions.BrokerUnreachableException
                            //NOTE: This is the only place where connections are created in the client
                            conn = connectionFactory.CreateConnection();

                            using (IModel channel = conn.CreateModel())
                            {
                                //Declare call back queue
                                var callbackQueueArgs = new System.Collections.Hashtable();
                                callbackQueueArgs.Add("x-expires", (long)Utils.GetCallbackQueueExpiry().TotalMilliseconds);

                                channel.QueueDeclare(callbackQueueName, false, false, true, callbackQueueArgs);

                                callbackConsumer = new QueueingBasicConsumer(channel);
                                channel.BasicConsume(callbackQueueName, false, callbackConsumer);

                                //Notify outer thread that channel has started consumption
                                consumerSignal.Set();

                                object obj;
                                global::RabbitMQ.Client.Events.BasicDeliverEventArgs evt;

                                while (true)
                                {

                                    try
                                    {
                                        obj = DequeueCallbackQueue();
                                    }
                                    catch
                                    {
                                        //TODO: Log this exception except it's ObjectDisposedException
                                        throw;
                                    }

                                    evt = (global::RabbitMQ.Client.Events.BasicDeliverEventArgs)obj;

                                    try
                                    {
                                        if (responseArrivalNotification != null)
                                        {
                                            responseArrivalNotification(evt);
                                        }
                                    }
                                    catch
                                    {
                                        //DO nothing
                                    }

                                    //Acknowledge receipt
                                    channel.BasicAck(evt.DeliveryTag, false);

                                }

                            }
                        }
                        catch
                        {
                            //Notify outer thread to move on, in case it's still waiting
                            try
                            {
                                consumerSignal.Set();
                            }
                            catch { }

                            //TODO: Log error (Except it's object disposed exception)
                            //TODO: Set Exception object which will be throw by signal waiter
                        }
                        finally
                        {
                            DisposeConnection();
                        }

                    });

                    //Start Thread
                    callBackProcessor.Name = "RestBus RabbitMQ Client Callback Queue Consumer";
                    callBackProcessor.IsBackground = true;
                    callBackProcessor.Start();

                    //Wait for Thread to start consuming messages
                    consumerSignal.Wait();

                    //TODO: Examine exception if it were set and rethrow it

                }
            }

        }


        protected override void Dispose(bool disposing)
        {

            //TODO: Work on this method

            //TODO: Confirm that this does in fact kill all background threads

            disposed = true;
            DisposeConnection();

            //TODO: Kill all channels in channel pool (if implemented)

            base.Dispose(disposing);

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

        private void DisposeConnection()
        {
            if (conn != null)
            {

                try
                {
                    conn.Close();
                }
                catch
                {
                    //TODO: Log Error
                }

                try
                {
                    conn.Dispose();
                }
                catch
                {
                    //TODO: Log Error
                }
            }
        }

        private object DequeueCallbackQueue()
        {
            while (true)
            {
                if (disposed) throw new ObjectDisposedException("Client has been disposed");

                object obj = callbackConsumer.Queue.DequeueNoWait(null);

                if (obj != null)
                {
                    return obj;
                }

                Thread.Sleep(1);
            }
        }

    }



}
