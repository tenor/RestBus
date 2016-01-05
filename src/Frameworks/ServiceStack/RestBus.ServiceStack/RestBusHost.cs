using RestBus.Common;
using ServiceStack.Messaging;
using ServiceStack.WebHost.Endpoints;
using ServiceStack.WebHost.Endpoints.Support;
using System;
using System.Collections.Generic;

namespace RestBus.ServiceStack
{
	public class RestBusHost : IMessageService
	{
        const string HTTP_RESPONSE_VERSION = "1.1";

		private IMessageHandler[] messageHandlers;
		public int RetryCount { get; protected set; }
		public Func<IMessage, IMessage> RequestFilter { get; set; }
		public Func<object, object> ResponseFilter { get; set; }
		private readonly Dictionary<Type, IMessageHandlerFactory> handlerMap
			= new Dictionary<Type, IMessageHandlerFactory>();

		private readonly IRestBusSubscriber subscriber;
		InterlockedBoolean hasStarted;
		volatile bool disposed = false;

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

		public List<Type> RegisteredTypes
		{
			get
			{
				return System.Linq.Enumerable.ToList( handlerMap.Keys);
			}
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
            if (!hasStarted.SetTrueIf(false))
            {
                throw new InvalidOperationException("RestBus host has already started!");
            }

            subscriber.Start();

			System.Threading.Thread msgLooper = new System.Threading.Thread(RunLoop);
			msgLooper.Name = "RestBus ServiceStack Host";
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
			disposed = true;
			if (subscriber != null)
			{
				subscriber.Dispose();
			}
		}

		private void RunLoop()
		{
			MessageContext context = null;
			while (true)
			{
				try
				{
					context = subscriber.Dequeue();
				}
				catch (Exception e)
				{
					if (!(e is ObjectDisposedException))
					{
						//TODO: Log exception: Don't know what else to expect here

					}

					//Exit method if host has been disposed
					if (disposed)
					{
						break;
					}
					else
					{
						continue;
					}
				}

				System.Threading.ThreadPool.QueueUserWorkItem(Process, context);

			}
		}

		private void Process(object state)
		{
			try
			{
				ProcessRequest((MessageContext)state);
			}
			catch
			{
                //TODO: SHouldn't happen: (the called method should be safe): Log execption
			}
		}

		private void ProcessRequest(MessageContext context)
		{
			//NOTE: This method is called on a background thread and must be protected by an outer big-try catch

			var httpReq = new RequestWrapper(context.Request);
            var httpRes = new ResponseWrapper();

			IServiceStackHttpHandler handler = null;
            string operationName, contentType;

            //var handler = ServiceStackHttpHandlerFactory.GetHandler(httpReq);

			var restPath = RestHandler.FindMatchingRestPath(httpReq.HttpMethod, httpReq.PathInfo, out contentType);
            if (restPath != null)
            {
                handler = new RestHandler { RestPath = restPath, RequestName = restPath.RequestType.Name };
                httpReq.OperationName = operationName = ((RestHandler)handler).RestPath.RequestType.Name;
            }
            else
            {
                handler = new NotFoundHttpHandler();
                var stream = httpRes.OutputStream; //Bug fix: reading the OutputStream property will cause it to be created if it's null
                httpReq.OperationName = operationName = null;
            }

            HttpResponsePacket resPacket = null;
            try
            {
                handler.ProcessRequest(httpReq, httpRes, operationName);
                resPacket = CreateResponsePacketFromWrapper(httpRes, subscriber);
            }
            catch (Exception exception)
            {
                //Send Exception details back to Queue
                resPacket = CreateResponsePacketFromException(exception);
            }
            finally
            {
                httpReq.InputStream.Close();
                httpRes.Close();
            }


            if (resPacket == null)
            {
                //TODO: Not good, Log this
                //TODO: derive exception from RestBus.Exceptions class
                resPacket = CreateResponsePacketFromException(new ApplicationException("Unable to get response"));
            }

            try
            {
                subscriber.SendResponse(context, resPacket);
            }
            catch
            {
                //TODO: Log SendResponse error
            }

		}

		private static HttpResponsePacket CreateResponsePacketFromWrapper(ResponseWrapper wrapper, IRestBusSubscriber subscriber)
		{
			HttpResponsePacket response = new HttpResponsePacket();

			string trimmedKey;
			foreach (string key in wrapper.Headers.AllKeys)
			{
				foreach (string value in wrapper.Headers.GetValues(key))
				{
					trimmedKey = key.Trim();
					if (trimmedKey != String.Empty)
					{
						if (response.Headers.ContainsKey(trimmedKey))
						{
							((List<string>)response.Headers[trimmedKey]).Add(value);
						}
						else
						{
							response.Headers.Add(trimmedKey, new List<string> { value });
						}
					}
				}
			}

            //TODO: Investigate if servicestack V3 produces a Server header, if so add it here and in CreateResponseFromException

			response.Content = (wrapper.OutputStream as System.IO.MemoryStream).ToArray();
			response.StatusCode = wrapper.StatusCode;
			response.StatusDescription = wrapper.StatusDescription;
            response.Version = HTTP_RESPONSE_VERSION;

			return response;
		}

        private HttpResponsePacket CreateResponsePacketFromException(Exception ex)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("Exception: \r\n\r\n");
            sb.Append(ex.Message);
            sb.Append("\r\n\r\nStackTrace: \r\n\r\n");
            sb.Append(ex.StackTrace);

            if (ex.InnerException != null)
            {
                sb.Append("Inner Exception: \r\n\r\n");
                sb.Append(ex.InnerException.Message);
                sb.Append("\r\n\r\nStackTrace: \r\n\r\n");
                sb.Append(ex.InnerException.StackTrace);
            }

            return new HttpResponsePacket
            {
                Content = new System.Text.UTF8Encoding().GetBytes(sb.ToString()),
                StatusCode = 500, //HttpStatusCode.InternalServerError
                StatusDescription = "An unexpected exception was thrown.",
                Version = HTTP_RESPONSE_VERSION
            };

            //TODO: Investigate if servicestack V3 produces a Server header, if so add it here

        }

    }
}
