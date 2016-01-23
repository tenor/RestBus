using RabbitMQ.Client.Framing;
using RestBus.Client;
using RestBus.Common;
using RestBus.Common.Amqp;
using RestBus.Common.Http;
using RestBus.RabbitMQ.ChannelPooling;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace RestBus.RabbitMQ.Client
{
    public class RestBusClient : MessageInvokerBase
    {
        static SequenceGenerator correlationIdGen = SequenceGenerator.FromUtcNow();

        readonly IMessageMapper messageMapper;
        readonly MessagingConfiguration messagingConfig;
        readonly ConnectionManager connectionMgr;
        readonly IRPCStrategy directStrategy;
        readonly IRPCStrategy callbackStrategy;

        readonly object exchangeDeclareSync = new object();
        volatile int lastExchangeDeclareTickCount = 0;
        volatile bool disposed = false;

        volatile bool hasKickStarted = false;
        private Uri baseAddress;
        private HttpRequestHeaders defaultRequestHeaders;
        private TimeSpan timeout;

        static readonly RabbitMQMessagingProperties _defaultMessagingProperties = new RabbitMQMessagingProperties();

        //TODO: Consider moving this into Common, Maybe Client (Requires a reference to System.Net.Http)
        internal static readonly ByteArrayContent _emptyByteArrayContent = new ByteArrayContent(new byte[0]);

        /// <summary>
        /// Initializes a new instance of the <see cref="T:RestBus.RabbitMQ.RestBusClient" /> class.
        /// </summary>
        /// <param name="messageMapper">The <see cref="IMessageMapper" /> the client uses to route messages.</param>
        public RestBusClient(IMessageMapper messageMapper) : this(messageMapper, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:RestBus.RabbitMQ.RestBusClient" /> class.
        /// </summary>
        /// <param name="messageMapper">The <see cref="IMessageMapper" /> the client uses to route messages.</param>
        /// <param name="settings">Client settings.</param>
        public RestBusClient(IMessageMapper messageMapper, ClientSettings settings ) : base(new HttpClientHandler(), true)
        {
            //Set default HttpClient related fields
            timeout = TimeSpan.FromSeconds(100);
            MaxResponseContentBufferSize = int.MaxValue;
            //TODO: Setup cancellation token here.

            //Configure RestBus fields/properties
            this.messageMapper = messageMapper;
            this.messagingConfig = messageMapper.GetMessagingConfig();
            if (messagingConfig == null) throw new ArgumentException("messageMapper.GetMessagingConfig() returned null");

            //Set ClientSettings
            this.Settings = settings ?? new ClientSettings(); // Always have a default instance, if it wasn't passed in.
            this.Settings.Client = this; //Indicate that the settings is owned by this client.

            //Instantiate connection manager and RPC strategies;
            connectionMgr = new ConnectionManager(messagingConfig);
            directStrategy = new DirectReplyToRPCStrategy();
            callbackStrategy = new CallbackQueueRPCStrategy(this.Settings, messageMapper.GetServiceName(null));
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

            if (disposed) throw new ObjectDisposedException(GetType().FullName);
            hasKickStarted = true;
            PrepareMessage(request);

            //Get Request Options
            RequestOptions requestOptions = GetRequestOptions(request);
            var messageProperties = GetMessagingProperties(requestOptions);

            //Determine if message expects a response
            TimeSpan requestTimeout = GetRequestTimeout(requestOptions);
            //TODO: expectingResponse has a slightly different meaning in publisher confirms 
            //where timespan may be longer than zero but MessageExpectsReply is false
            //in which case the timeout only applies to how long to wait for the publisher confirmation.
            bool expectingResponse = requestTimeout != TimeSpan.Zero && GetExpectsReply(request);

            //Declare messaging resources
            ExpectedResponse arrival = null;
            AmqpModelContainer model = null;
            bool modelClosed = false;
            string correlationId = null;

            //Get channel pool and decide on RPC strategy
            var pool = connectionMgr.GetConnectedPool();
            IRPCStrategy rpcStrategy = pool.IsDirectReplyToCapable && !Settings.DisableDirectReplies ? directStrategy : callbackStrategy;

            try
            {
                #region Ensure CallbackQueue is started (If Using CallbackQueue Strategy)

                rpcStrategy.StartStrategy(pool, expectingResponse);

                #endregion

                #region Populate BasicProperties
                //Fill BasicProperties

                BasicProperties basicProperties = new BasicProperties();

                //Set message delivery mode -- Make message persistent if either:
                // 1. Properties.Persistent is true
                // 2. messagingConfig.PersistentMessages is true and Properties.Persistent is null
                // 3. messagingConfig.PersistentMessages is true and Properties.Persistent is true
                if (messageProperties.Persistent == true || (messagingConfig.PersistentMessages && messageProperties.Persistent != false))
                {
                    basicProperties.Persistent = true;
                }

                //Set Exchange Headers
                var exchangeHeaders = messageProperties.Headers ?? messageMapper.GetHeaders(request);
                if (exchangeHeaders != null)
                {
                    basicProperties.Headers = exchangeHeaders;
                }

                if (expectingResponse)
                {
                    //Set CorrelationId
                    correlationId = correlationIdGen.GetNextId();
                    basicProperties.CorrelationId = correlationId;

                    //Set Expiration if messageProperties doesn't override Client.Timeout, RequestOptions and MessageMapper.
                    if (!messageProperties.Expiration.HasValue && requestTimeout != System.Threading.Timeout.InfiniteTimeSpan 
                        && ( messagingConfig.MessageExpires == null || messagingConfig.MessageExpires(request)))
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
                }
                else if (!messageProperties.Expiration.HasValue && (messagingConfig.MessageExpires == null || messagingConfig.MessageExpires(request)))
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

                #endregion

                #region Get Ready to Send Message

                model = rpcStrategy.GetModel(pool, false);

                var serviceName =
                    (requestOptions == null || requestOptions.ServiceName == null)
                    ? (messageMapper.GetServiceName(request) ?? String.Empty).Trim()
                    : requestOptions.ServiceName.Trim();

                RedeclareExchangesAndQueues(model, serviceName);

                //TODO: Check if cancellation token was set before operation even began
                var taskSource = new TaskCompletionSource<HttpResponseMessage>();

                var exchangeKind = ExchangeKind.Direct;
                //TODO: Get ExchangeKind from CLient.Settings.ExchangeKind
                //TODO: Pull exchangeName from a concurrent dictionary that has a key of serviceName, exchangeKind
                //exchangeKind could be an index into arrays that have concurrentDictionaries.
                var exchangeName = AmqpUtils.GetExchangeName(messagingConfig, serviceName, exchangeKind);

                #endregion

                #region Start waiting for response
                //Start waiting for response if a request timeout is set.
                if (expectingResponse)
                {
                    //TODO: Better to just check if cancellationHasbeen requested instead of checking if it's None
                    if (!cancellationToken.Equals(System.Threading.CancellationToken.None))
                    {
                        //TODO: Have cancellationtokens cancel event trigger callbackHandle
                        //In fact turn this whole thing into an extension
                    }

                    arrival = rpcStrategy.PrepareForResponse(correlationId, basicProperties, model, request, requestTimeout, cancellationToken, taskSource);

                }

                #endregion

                #region Send Message
                //TODO: Implement routing to a different exchangeKind via substituting exchangeName
                //Send message
                model.Channel.BasicPublish(exchangeName,
                                messageProperties.RoutingKey ?? messageMapper.GetRoutingKey(request, exchangeKind) ?? AmqpUtils.GetWorkQueueRoutingKey(),
                                basicProperties,
                                request.ToHttpRequestPacket().Serialize());

                //Close channel
                if (!expectingResponse || rpcStrategy.ReturnModelAfterSending)
                {
                    CloseAmqpModel(model);
                    modelClosed = true;
                }

                #endregion

                #region Cleanup if not expecting response
                //Exit with OK result if no request timeout was set.
                if (!expectingResponse)
                {
                    //TODO: Investigate adding a publisher confirm for zero timeout messages so we know that RabbitMQ did pick up the message before replying OK.

                    //Zero timespan means the client isn't interested in a response
                    taskSource.SetResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = _emptyByteArrayContent });

                    rpcStrategy.CleanupMessagingResources(correlationId, arrival);
                }

                #endregion

                return taskSource.Task;

            }
            catch (Exception ex)
            {
                //TODO: Log this

                if (model != null && !modelClosed)
                {
                    if (expectingResponse && model.Flags == ChannelFlags.RPC || model.Flags == ChannelFlags.RPCWithPublisherConfirms)
                    {
                        //Model might still be in use in waiting thread and so unsafe to be recycled
                        model.Discard = true;
                    }

                    CloseAmqpModel(model);
                }

                rpcStrategy.CleanupMessagingResources(correlationId, arrival);

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
            disposed = true;
            directStrategy.Dispose();
            callbackStrategy.Dispose();
            connectionMgr.Dispose();

            base.Dispose(disposing);
        }

        private void RedeclareExchangesAndQueues(AmqpModelContainer model, string serviceName)
        {
            //Redeclare exchanges and queues every minute if exchanges and queues are transient, or the first time client is sending a message
            TimeSpan elapsedSinceLastDeclareExchange = TimeSpan.FromMilliseconds(Environment.TickCount - lastExchangeDeclareTickCount);

            //TODO: Partition elapsedSinceLastDeclareExchange by serviceName and connection so that redeclares take place on new servicenames and connections.

            //Discovering firstDeclare by comparing lastExchangeDeclareTickCount to zero is not perfect 
            //because tickcount can wrap back to zero (through the negative number range), if client is running long enough.
            //However, redeclaring exchanges and queues are a safe operation, so this is okay if it occurs more than once in persistent queues.
            bool firstDeclare = lastExchangeDeclareTickCount == 0;
            if (firstDeclare || (!messagingConfig.PersistentWorkQueuesAndExchanges && (elapsedSinceLastDeclareExchange.TotalMilliseconds < 0 || elapsedSinceLastDeclareExchange.TotalSeconds > 60)))
            {
                if (!firstDeclare)
                {
                    //All threads must attempt to declare exchanges and queues if it hasn't been previously declared 
                    //(for instance, all threads were started at once)
                    //So do not swap out this value on first declare
                    lastExchangeDeclareTickCount = Environment.TickCount;
                }
                AmqpUtils.DeclareExchangeAndQueues(model.Channel, messagingConfig, serviceName, exchangeDeclareSync, null);
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

        private TimeSpan GetRequestTimeout(RequestOptions options)
        {
            TimeSpan timeoutVal = this.Timeout;

            if (options != null && options.Timeout.HasValue)
            {
                timeoutVal = options.Timeout.Value;
            }

            return timeoutVal.Duration();
        }

        private bool GetExpectsReply(HttpRequestMessage request)
        {
            var options = GetRequestOptions(request);
            if (options == null || options.ExpectsReply == null)
            {
                return messagingConfig.MessageExpectsReply == null ? true : messagingConfig.MessageExpectsReply(request);
            }
            else
            {
                return options.ExpectsReply.Value;
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

        internal static HttpRequestException GetWrappedException(string message, Exception innerException)
        {
            return new HttpRequestException(message, innerException);
        }

        private static RabbitMQMessagingProperties GetMessagingProperties(RequestOptions options)
        {
            if (options == null) return _defaultMessagingProperties;
            return (options.Properties as RabbitMQMessagingProperties) ?? _defaultMessagingProperties;
        }
    }



}
