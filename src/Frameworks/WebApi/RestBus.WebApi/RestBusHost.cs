using RestBus.Common;
using RestBus.Common.Http;
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
        private static string machineHostName;
        readonly static string[] HTTP_RESPONSE_SERVER_HEADER = new string[] { "RestBus.WebApi" };
        private readonly IRestBusSubscriber subscriber;
        private readonly HttpConfiguration config;
        private readonly RequestHandler requestHandler;
        private string appVirtualPath;
        InterlockedBoolean hasStarted;
        volatile bool disposed = false;

        //TODO: Consider moving this into Common, Maybe Client (Requires a reference to System.Net.Http)
        internal static readonly ByteArrayContent _emptyByteArrayContent = new ByteArrayContent(new byte[0]);

        //TODO: Consider moving this to Common
        internal static Version VERSION_1_1 = new Version("1.1");

        public RestBusHost(IRestBusSubscriber subscriber, HttpConfiguration config)
        {
            this.subscriber = subscriber;
            this.config = config;
            this.requestHandler = new RequestHandler(config);
        }


        public void Start()
        {
            if (!hasStarted.SetTrueIf(false))
            {
                throw new InvalidOperationException("RestBus host has already started!");
            }
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
                    if (!(e is ObjectDisposedException || e is OperationCanceledException))
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
                //TODO: SHouldn't occur (the called method should be safe): Log execption and return a server error
            }
        }

        private async Task ProcessRequest(MessageContext restbusContext, CancellationToken cancellationToken)
        {
            //NOTE: This method is called on a background thread and must be protected by an outer big-try catch

            try
            {
                if (machineHostName == null) machineHostName = Environment.MachineName;
            }
            catch { }

            HttpRequestMessage requestMsg;
            HttpResponseMessage responseMsg = null;

            if (!TryGetHttpRequestMessage(restbusContext.Request, appVirtualPath ?? (appVirtualPath = System.Web.Hosting.HostingEnvironment.ApplicationVirtualPath), machineHostName, out requestMsg))
            {
                responseMsg = new HttpResponseMessage(HttpStatusCode.BadRequest) { ReasonPhrase = "Bad Request" };
            }
            else
            {
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
            }


            //Send Response
            try
            {
                subscriber.SendResponse(restbusContext, CreateResponsePacketFromMessage(responseMsg, subscriber));
            }
            catch
            {
                //TODO: Log SendResponse error
            }
        }

        private static bool TryGetHttpRequestMessage(HttpRequestPacket packet, string virtualPath, string hostname, out HttpRequestMessage request)
        {
            try
            {
                request = new HttpRequestMessage
                {
                    Content = packet.Content == null ? _emptyByteArrayContent : new ByteArrayContent(packet.Content),
                    Version = packet.Version == "1.1" ? VERSION_1_1 : new Version(packet.Version),
                    Method = new HttpMethod(packet.Method ?? "GET"),
                    RequestUri = packet.BuildUri(virtualPath, hostname)
                };

                packet.PopulateHeaders(request.Content.Headers, request.Headers);
            }
            catch
            {
                request = null;
                return false;
            }

            return true;
        }

        private static HttpResponsePacket CreateResponsePacketFromMessage(HttpResponseMessage responseMsg, IRestBusSubscriber subscriber)
        {
            var responsePkt = responseMsg.ToHttpResponsePacket();

            //Add/Update Server header
            responsePkt.Headers["Server"] = HTTP_RESPONSE_SERVER_HEADER;

            return responsePkt;
        }

        private static HttpResponseMessage CreateResponseMessageFromException(Exception ex)
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
                ReasonPhrase = "An unexpected exception was thrown."
            };

        }
    }
}
