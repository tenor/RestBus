using RestBus.RabbitMQ;
using RestBus.RabbitMQ.Subscriber;
using ServiceStack.Messaging;
using ServiceStack.WebHost.Endpoints;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RestBus.ServiceStack
{
    public class RestBusHost : IMessageService
    {
        private IMessageHandler[] messageHandlers;
        public int RetryCount { get; protected set; }
        public Func<IMessage, IMessage> RequestFilter { get; set; }
        public Func<object, object> ResponseFilter { get; set; }
        private readonly Dictionary<Type, IMessageHandlerFactory> handlerMap
            = new Dictionary<Type, IMessageHandlerFactory>();

        private IRestBusSubscriber subscriber;


        private bool hasStarted = false;

        public RestBusHost(IRestBusSubscriber subscriber)
        {
            this.subscriber = subscriber;
        }


        public IMessageHandlerStats GetStats()
        {
            //TODO:
            throw new NotImplementedException();
        }

        public string GetStatsDescription()
        {
            //TODO:
            throw new NotImplementedException();
        }

        public string GetStatus()
        {
            //TODO:
            throw new NotImplementedException();
        }

        public IMessageFactory MessageFactory
        {
            get;
            private set;
        }

        public void RegisterHandler<T>(Func<IMessage<T>, object> processMessageFn, Action<IMessage<T>, Exception> processExceptionEx)
        {
            if (handlerMap.ContainsKey(typeof(T)))
            {
                throw new ArgumentException("Message handler has already been registered for type: " + typeof(T).Name);
            }

            handlerMap[typeof(T)] = CreateMessageHandlerFactory(processMessageFn, processExceptionEx);
        }

        public void RegisterHandler<T>(Func<IMessage<T>, object> processMessageFn)
        {
            RegisterHandler(processMessageFn, null);
        }

        protected IMessageHandlerFactory CreateMessageHandlerFactory<T>(Func<IMessage<T>, object> processMessageFn, Action<IMessage<T>, Exception> processExceptionEx)
        {
            return new MessageHandlerFactory<T>(this, processMessageFn, processExceptionEx)
            {
                RequestFilter = this.RequestFilter,
                ResponseFilter = this.ResponseFilter,
                RetryCount = this.RetryCount,
            };
        }


        public void Start()
        {
            if (hasStarted) return;

            //TODO: Add some sync here so that multiple threads are not created.
            hasStarted = true;
            subscriber.Start();


            System.Threading.Thread msgLooper = new System.Threading.Thread(RunLoop);
            msgLooper.IsBackground = true;
            msgLooper.Start();

        }

        public void Stop()
        {
            //TODO: Add a flag that will automatically end runloop
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            //TODO:
            throw new NotImplementedException();
        }

        private void RunLoop()
        {
            HttpContext context = null;
            while (true)
            {
                //TODO: Host shouldn't be aware of EndOfStreamException which belongs to RabbitMQ

                try
                {
                    context = subscriber.Dequeue();
                }
                catch (System.IO.EndOfStreamException)
                {
                    //TODO: Log this exception
                    subscriber.Restart();
                    continue;
                }

                //TODO: ProcessRequest should happen on the ThreadPool and there should be some sort of flow control
                ProcessRequest(context);
            }
        }

        private void ProcessRequest(HttpContext context)
        {
			//if (string.IsNullOrEmpty(context.Request.resource)) return;

			//var operationName = context.Request.GetOperationName();

			var httpReq = new RequestWrapper(context.Request);
			var httpRes = new ResponseWrapper(context);
			//var handler = ServiceStackHttpHandlerFactory.GetHandler(httpReq);

            RestHandler handler = null;

            var restPath = RestHandler.FindMatchingRestPath(httpReq.HttpMethod, httpReq.PathInfo);
            if (restPath != null)
            {
                handler = new RestHandler { RestPath = restPath, RequestName = restPath.RequestType.Name };

                string operationName;
                httpReq.OperationName = operationName = handler.RestPath.RequestType.Name;

                try
                {
                    handler.ProcessRequest(httpReq, httpRes, operationName);
                }
                catch
                {
                    //TODO: Send Exception details back to Queue
                }
                finally
                {
                    httpReq.InputStream.Close();
                    httpRes.Close();
                }

                try
                {
                    subscriber.SendResponse(context, GetResponsePacketFromWrapper(httpRes));
                }
                catch
                {
                    //Log SendResponse error
                }

                return;
            }


            //TODO: Send this exception back to Queue
            throw new ApplicationException("The Resource cannot be found");


		}

        private HttpResponsePacket GetResponsePacketFromWrapper(ResponseWrapper wrapper)
        {
            HttpResponsePacket response = new HttpResponsePacket();
            foreach (var key in wrapper.Headers.AllKeys)
            {
                //TODO: Confirm if headers splits into comma here
                //TODO: WHat happens in a set-cookie type of situation where you can have multiple headers of the same name?
                response.Headers.Add(key, new string[]{ wrapper.Headers[key]});
            }


            response.Content = (wrapper.OutputStream as System.IO.MemoryStream).ToArray();
            response.StatusCode = wrapper.StatusCode;
            response.StatusDescription = wrapper.StatusDescription;
            response.Version = "1.1";

            return response;
        }
    
    }
}
