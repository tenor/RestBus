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
        internal const string REQUEST_OPTIONS_KEY = "_rb_options";

        readonly IExchangeMapper exchangeMapper;
        readonly ExchangeInfo exchangeInfo;
        readonly string clientId;
        readonly string exchangeName;
        readonly string workQueueName;
        readonly string callbackQueueName;
        readonly ConnectionFactory connectionFactory;
        QueueingBasicConsumer callbackConsumer = null;
        readonly object callbackConsumerStartSync = new object();
        event Action<global::RabbitMQ.Client.Events.BasicDeliverEventArgs> responseArrivalNotification = null;

        readonly TimeSpan workQueueExpiry;
        readonly TimeSpan callbackQueueExpiry;

        object exchangeDeclareSync = new object();
        int lastExchangeDeclareTickCount = 0;

        static string[] contentHeaders = { "ALLOW", "CONTENT-DISPOSITION", "CONTENT-ENCODING", "CONTENT-LANGUAGE", "CONTENT-LOCATION", "CONTENT-MD5", 
                                             "CONTENT-RANGE", "CONTENT-TYPE", "EXPIRES", "LAST-MODIFIED", "CONTENT-LENGTH"  };

        public RestBusClient(IExchangeMapper exchangeMapper) : base()
        {
            this.exchangeMapper = exchangeMapper;
            this.exchangeInfo = exchangeMapper.GetExchangeInfo();
            this.clientId = Utils.GetRandomId();
            this.exchangeName = Utils.GetExchangeName(exchangeInfo);
            this.workQueueName = Utils.GetWorkQueueName(exchangeInfo);
            this.callbackQueueName = Utils.GetCallbackQueueName(exchangeInfo, clientId);
            this.WaitForResponse = true;

            workQueueExpiry = Utils.GetWorkQueueExpiry();
            callbackQueueExpiry = Utils.GetCallbackQueueExpiry();

            //Map request to RabbitMQ Host and exchange, 
            this.connectionFactory = new ConnectionFactory();
            connectionFactory.Uri = exchangeInfo.ServerAddress;
        }

        //TODO: FIgure out how to handle the CancelPendingRequests() method (It's not overridable)

        //TODO: This method has to be thread safe
        public override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException("request");

            if (request.RequestUri == null && BaseAddress == null )
            {
               throw new InvalidOperationException("The request URI must either be set or BaseAddress must be set");
            }

            //TODO: Check if client is disposed

            IConnection conn = null;
            IModel channel = null;
            Action<global::RabbitMQ.Client.Events.BasicDeliverEventArgs> arrival = null;
            ManualResetEventSlim receivedEvent = null;

            PrepareMessage(request);

            //Get Request Options
            RequestOptions requestOptions = null;
            {
                object reqObj;
                if (request.Properties.TryGetValue(RestBusClient.REQUEST_OPTIONS_KEY, out reqObj))
                {
                    requestOptions = (RequestOptions)reqObj;
                }
            }


            try
            {
                //TODO: Think about connection pooling
                //or simply have one connection per client (since the consumer needs a connection anyway)
                //And have SendAsync use the same connection

                //TODO: Prepare for BrokerUnreachableException
                conn = GetConnectionFromPool();

                //P.S: Do not share channels across threads.
                channel = conn.CreateModel();

                TimeSpan elapsedSinceLastDeclareExchange = TimeSpan.FromMilliseconds(Environment.TickCount - lastExchangeDeclareTickCount);
                if (lastExchangeDeclareTickCount == 0 || elapsedSinceLastDeclareExchange.TotalMilliseconds < 0 || elapsedSinceLastDeclareExchange.TotalSeconds > 30)
                {
                    //Redeclare exchanges and queues every 30 seconds

                    Interlocked.Exchange(ref lastExchangeDeclareTickCount, Environment.TickCount);
                    DeclareExchangeAndQueues(channel);
                }

                //TODO: If client is only sending one way messages, do we still need a consumer queue
                //Note that consumer queue shares the same connection with the client in case you decide to implement this feature.

                if (callbackConsumer == null || !callbackConsumer.IsRunning)
                {
                    StartCallbackQueueConsumer();
                }


                //TODO: if exchangeInfo wants a Session/Server/Sticky Queue

                string correlationId = Utils.GetRandomId();
                BasicProperties basicProperties = new BasicProperties { CorrelationId = correlationId };

                //TODO: Check if it was 4 minutes since last callback queue was established and redeclare callbackqueue

                //DO Sane for exchangeQueue

                //TODO: Check if cancellation token was set before operation even began

                var taskSource = new TaskCompletionSource<HttpResponseMessage>();

                TimeSpan requestTimeout = GetRequestTimeout(requestOptions);

                if (requestTimeout == TimeSpan.Zero)
                {
                    //TODO: Investigate adding a publisher confirm for zero timeout messages so we know that RabbitMQ did pick up the message before relying OK.

                    //Zero timespan means the client isn't interested in a response
                    taskSource.SetResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK){ Content = new ByteArrayContent(new byte[0])});
                }
                else
                {
                    basicProperties.ReplyTo = callbackQueueName;
                    if (!IsRequestTimeoutInfinite(requestOptions) && exchangeMapper.GetExpires(request))
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
                    HttpResponsePacket responseEnv = null;
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
                                res = Utils.Deserialize<HttpResponsePacket>(a.Body);
                            }
                            catch
                            {
                                deserializationError = true;
                            }
                            //TODO: Add Content-Length Header

                            if (!deserializationError)
                            {
                                responseEnv = res;
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
                                        if (!deserializationError && TryGetResponseFromEnvelope(responseEnv, out msg))
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
                                    //TODO: HAve a helper method that both dispose routines will call

                                    try
                                    {
                                        if (channel != null)
                                        {
                                            channel.Dispose();
                                        }

                                    }
                                    catch { }

                                    try
                                    {
                                        if (conn != null)
                                        {
                                            ReturnConnectionToPool(conn);
                                        }
                                    }
                                    catch { }

                                    try
                                    {
                                        if (arrival != null)
                                        {
                                            responseArrivalNotification -= arrival;
                                        }
                                    }
                                    catch { }

                                    if (receivedEvent != null)
                                    {
                                        receivedEvent.Dispose();
                                    }
                                }
                            },
                                null,
                                IsRequestTimeoutInfinite(requestOptions) ? -1 : (long)requestTimeout.TotalMilliseconds,
                                true);

                    }

                    responseArrivalNotification += arrival;
                }

                channel.BasicPublish(exchangeName,
                                exchangeMapper.GetRoutingKey(request) ?? exchangeName,
                                basicProperties,
                                Utils.Serialize(new HttpRequestPacket(request)));

                //TODO: Can you make calls to Wait() not loop for change and instead rely on Kernel for notification

                //NOTE that once you leave this method, the callback queue is closed and client can not receive any message s on that queue.

                return taskSource.Task;

            }
            catch
            {
                //TODO: Log this

                if (channel != null)
                {
                    channel.Dispose();
                }

                if (conn != null)
                {
                    ReturnConnectionToPool(conn);
                }

                try
                {
                    if (arrival != null)
                    {
                        responseArrivalNotification -= arrival;
                    }
                }
                catch { }

                if (receivedEvent != null)
                {
                    receivedEvent.Dispose();
                }

                throw;

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


        private void DeclareExchangeAndQueues(IModel channel)
        {
            lock (exchangeDeclareSync)
            {
                if (exchangeInfo.Exchange != "")
                {
                    channel.ExchangeDeclare(exchangeName, exchangeInfo.ExchangeType);
                }

                var workQueueArgs = new System.Collections.Hashtable();
                workQueueArgs.Add("x-expires", (long)workQueueExpiry.TotalMilliseconds);

                //Declare work queue
                channel.QueueDeclare(workQueueName, false, false, false, workQueueArgs);
                channel.QueueBind(workQueueName, exchangeName, exchangeName);


            }
        }

        //TODO: If COnnection fails this needs to be restarted
        //Note that it creates a new background thread which would have to be stopped before restarting
        //Also note that connection is seperate from client connection
        private void StartCallbackQueueConsumer()
        {
            lock (callbackConsumerStartSync)
            {
                if (callbackConsumer != null && callbackConsumer.IsRunning) return;

                //This method waits on this signal to make sure the callbackprocessor thread either started successfully or failed.
                ManualResetEventSlim consumerSignal = new ManualResetEventSlim(false);

                Thread callBackProcessor = new Thread(p =>
                {

                    IConnection conn = null;
                    try
                    {
                        try
                        {
                            conn = GetConnectionFromPool();
                        }
                        catch(global::RabbitMQ.Client.Exceptions.BrokerUnreachableException)
                        {
                            //TODO: Signal main thread that callbackqueue is dead
                            //So that it may try again during the next send
                            return;
                        }

                        try
                        {
                            using (IModel channel = conn.CreateModel())
                            {
                                //Declare call back queue
                                var callbackQueueArgs = new System.Collections.Hashtable();
                                callbackQueueArgs.Add("x-expires", (long)callbackQueueExpiry.TotalMilliseconds);

                                channel.QueueDeclare(callbackQueueName, false, false, true, callbackQueueArgs);

                                callbackConsumer = new QueueingBasicConsumer(channel);
                                channel.BasicConsume(callbackQueueName, false, callbackConsumer);

                                //Notify outer thread that channel has started consumption
                                consumerSignal.Set();

                                object obj;
                                global::RabbitMQ.Client.Events.BasicDeliverEventArgs evt;

                                while (true)
                                {
                                    //TODO: Check for object disposal here and leave
                                    //This means Dequeue will have to be changed to one that loops and checks for the disposed flag

                                    obj = callbackConsumer.Queue.Dequeue();
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
                            //Notify outer thread, in case it's still waiting
                            consumerSignal.Set();

                            //TODO: Log error
                            //TODO: Inform class that CallbackQueue is dead, so that it can try again on next send
                        }

                    }
                    finally
                    {

                        //TODO: Test if CallBackConsumer.IsRunning is true at this point

                        if (conn != null)
                        {
                            ReturnConnectionToPool(conn);
                        }

                    }

                });

                //Start Thread
                callBackProcessor.IsBackground = true;
                callBackProcessor.Start();

                //Wait for Thread to start consuming messages
                consumerSignal.Wait();

            }

        }

        private static bool TryGetResponseFromEnvelope(HttpResponsePacket envelope, out HttpResponseMessage response)
        {
            try
            {
                response = new HttpResponseMessage
                {
                    Content = new ByteArrayContent(envelope.Content ?? new byte[0]),
                    Version = new Version(envelope.Version),
                    ReasonPhrase = envelope.StatusDescription,
                    StatusCode = (System.Net.HttpStatusCode)envelope.StatusCode
                };

                string hdrKey;
                foreach (var hdr in envelope.Headers)
                {
                    if(hdr.Key == null) continue;

                    hdrKey = hdr.Key.Trim().ToUpperInvariant();

                    if(hdrKey == "CONTENT-LENGTH") continue; //Content Length is automaitically calculated

                    if (Array.IndexOf<String>(contentHeaders, hdrKey) >= 0)
                    {
                        response.Content.Headers.Add(hdr.Key.Trim(), hdr.Value);
                    }
                    else
                    {
                        response.Headers.Add(hdr.Key.Trim(), hdr.Value);
                    }

                    //TODO: Check if a string can be parsed properly into the typed header

                    //Test adding multiple headers of the same name will do. // Look up the Add overload that takes an ienumerable<string> to figure out its purpose.
                }
            }
            catch
            {
                response = null;
                return false;
            }

            return true;
        }



        protected override void Dispose(bool disposing)
        {
            //TODO: Work on this method
            base.Dispose(disposing);

            //Kill all connections in Pool
        }

        //TODO: GetConnectionFromPool can always throw BrokerUnreachableException so keep that in mind when calling
        protected IConnection GetConnectionFromPool()
        {
            //TODO: It seems like connections can be shared across threads, in that case, simply create one connection and kill it only if it was created a long time ago
            //TODO: If it's a brand new connection then DeclareExchangesAndQueues all over
            return connectionFactory.CreateConnection();
        }

        protected void ReturnConnectionToPool(IConnection connection)
        {
            try
            {
                connection.Dispose();
            }
            catch
            {
                //TODO: Log Error
            }
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

    }



}
