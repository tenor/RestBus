using RestBus.Common;
using System;
using System.Net;
using System.Net.Http;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Controllers;

namespace RestBus.WebApi
{
    public class RestBusHost : IDisposable
    {
        private static readonly Lazy<IPrincipal> anonymousPrincipal = new Lazy<IPrincipal>(() => new GenericPrincipal(new GenericIdentity(String.Empty), new string[0]), isThreadSafe: true);
        private readonly IRestBusSubscriber subscriber;
        private readonly HttpConfiguration config;
        private readonly RequestHandler requestHandler;
        private string appVirtualPath;
        private bool hasStarted = false;


        //TODO: Switch this to something you can do compareExchange with in both hosts
        volatile bool disposed = false;

        public RestBusHost(IRestBusSubscriber subscriber, HttpConfiguration config)
        {
            this.subscriber = subscriber;
            this.config = config;
            this.requestHandler = new RequestHandler(config);
        }


        public void Start()
        {
            if (hasStarted) return;

            //TODO: Add some sync here so that multiple threads are not created.
            hasStarted = true;
            subscriber.Start();

            System.Threading.Thread msgLooper = new System.Threading.Thread(RunLoop);
            msgLooper.Name = "RestBus WebApi Host";
            msgLooper.IsBackground = true;
            msgLooper.Start();
        }

        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                requestHandler.Dispose();
                //configuration is disposed by requesthandler
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

                var cancellationToken = CancellationToken.None;
                Task.Factory.StartNew((Func<object, Task>)Process, Tuple.Create(context, cancellationToken), cancellationToken);

            }
        }

        private async Task Process(object state)
        {
            try
            {
                var typedState = (Tuple<MessageContext, CancellationToken>) state;
                await ProcessRequest(typedState.Item1, typedState.Item2);
            }
            catch (Exception ex)
            {
                //TODO: SHouldn't occur (the called methpd should be safe): Log execption and return a server error
            }
        }

        private async Task ProcessRequest(MessageContext restbusContext, CancellationToken cancellationToken)
        {
            //NOTE: This method is called on a background thread and must be protected by an outer big-try catch

            HttpRequestMessage requestMsg;
            HttpResponseMessage responseMsg = null;

            if (!restbusContext.Request.TryGetHttpRequestMessage(appVirtualPath ?? (appVirtualPath = System.Web.Hosting.HostingEnvironment.ApplicationVirtualPath), out requestMsg))
            {
                responseMsg = new HttpResponseMessage(HttpStatusCode.BadRequest) { ReasonPhrase = "Bad Request" };
            }


            if (disposed)
            {
                responseMsg = requestMsg.CreateErrorResponse(HttpStatusCode.ServiceUnavailable, "The server is no longer available.");
            }
            else
            {
                requestHandler.EnsureInitialized();

                // Add current synchronization context to request parameter
                SynchronizationContext syncContext = SynchronizationContext.Current;
                if (syncContext != null)
                {
                    requestMsg.SetSynchronizationContext(syncContext);
                }

                // Add HttpConfiguration to request parameter
                requestMsg.SetConfiguration(config);

                // Ensure we have a principal, even if the host didn't give us one
                IPrincipal originalPrincipal = Thread.CurrentPrincipal;
                if (originalPrincipal == null)
                {
                    Thread.CurrentPrincipal = anonymousPrincipal.Value;
                }

                // Ensure we have a principal on the request context (if there is a request context).
                HttpRequestContext requestContext = requestMsg.GetRequestContext();

                if (requestContext == null)
                {
                    requestContext = new RequestBackedHttpRequestContext(requestMsg);

                    // if the host did not set a request context we will also set it back to the request.
                    requestMsg.SetRequestContext(requestContext);
                }

                try
                {

                    try
                    {
                        responseMsg = await requestHandler.SendMessageAsync(requestMsg, cancellationToken);
                    }
                    catch (HttpResponseException exception)
                    {
                        responseMsg = exception.Response;
                    }
                    catch (NullReferenceException exception)
                    {
                        // There is a bug in older versions of HttpRoutingDispatcher which causes a null reference exception when
                        // a route could not be found
                        // This bug can be triggered by sending a request for a url that doesn't have a route
                        // This commit fixes the bug https://github.com/ASP-NET-MVC/aspnetwebstack/commit/6a0c03f9e549966a7f806f8b696ec4cb2ec272e6#diff-c89c7bee3d225a037a6d04e8e4447460

                        if (exception.TargetSite != null && exception.TargetSite.DeclaringType != null
                            && exception.TargetSite.DeclaringType.FullName == "System.Web.Http.Dispatcher.HttpRoutingDispatcher"
                            && exception.TargetSite.Name == "SendAsync")
                        {
                            //This is the bug, so send a 404 instead

                            const string NoRouteMatchedHttpPropertyKey = "MS_NoRouteMatched";

                            requestMsg.Properties.Add(NoRouteMatchedHttpPropertyKey, true);
                            responseMsg = requestMsg.CreateErrorResponse(
                                HttpStatusCode.NotFound,
                                String.Format("No HTTP resource was found that matches the request URI '{0}'.", requestMsg.RequestUri));

                        }
                        else
                        {
                            responseMsg = CreateResponseMessageFromException(exception);
                        }
                    }
                    catch (Exception exception)
                    {
                        responseMsg = CreateResponseMessageFromException(exception);
                    }

                    if (responseMsg == null)
                    {
                        //TODO: Not good, Log this
                        //TODO: derive exception from RestBus.Exceptions class
                        responseMsg = CreateResponseMessageFromException(new ApplicationException("Unable to get response"));
                    }

                }
                finally
                {
                    Thread.CurrentPrincipal = originalPrincipal;
                }
            }


            //Send Response
            try
            {
                //TODO: Why can't the subscriber append the subscriber id itself from within sendresponse
                subscriber.SendResponse(restbusContext, CreateResponsePacketFromMessage(responseMsg, subscriber));
            }
            catch
            {
                //TODO: Log SendResponse error
            }
        }

        private HttpResponsePacket CreateResponsePacketFromMessage(HttpResponseMessage responseMsg, IRestBusSubscriber subscriber)
        {
            //TODO: Confirm that commas in response headers are merged iproperly into packet header
            var responsePkt = new HttpResponsePacket(responseMsg);

            //Add/Update Subscriber-Id header
            responsePkt.Headers[Common.Shared.SUBSCRIBER_ID_HEADER] = new string[] { subscriber == null ? String.Empty : subscriber.Id ?? String.Empty };

            return responsePkt;
        }

        private HttpResponseMessage CreateResponseMessageFromException(Exception ex)
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

            return new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent(sb.ToString()),
                ReasonPhrase = "An unexpected exception was thrown"
            };

        }
    }
}
