using RestBus.Common;
using System;
using System.Net.Http;
using System.Security.Principal;
using System.Threading;
using System.Web.Http;
using System.Web.Http.Hosting;

namespace RestBus.WebApi
{
    public class RestBusHost : IDisposable
    {
        private static readonly Lazy<IPrincipal> anonymousPrincipal = new Lazy<IPrincipal>(() => new GenericPrincipal(new GenericIdentity(String.Empty), new string[0]), isThreadSafe: true);
        private readonly IRestBusSubscriber subscriber;
        private readonly HttpConfiguration config;
        private readonly RequestHandler requestHandler;
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
            HttpContext context = null;
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
                ProcessRequest((HttpContext)state);
            }
            catch (Exception ex)
            {
                //TODO: SHouldn't occur: Log execption
            }
        }

        private void ProcessRequest(HttpContext context)
        {
            //NOTE: This method is called on a background thread and must be protected by a big-try catch

            HttpRequestMessage requestMsg;
            if (!context.Request.TryGetHttpRequestMessage(out requestMsg))
            {
                //TODO: Return Bad Request Response
            }

            //TODO: Protect this statement with a Try Catch
            requestHandler.EnsureInitialized();

            // Add current synchronization context to request parameter
            SynchronizationContext syncContext = SynchronizationContext.Current;
            if (context != null)
            {
                requestMsg.Properties.Add(HttpPropertyKeys.SynchronizationContextKey, syncContext);
            }

            // Add HttpConfiguration to request parameter
            requestMsg.Properties.Add(HttpPropertyKeys.HttpConfigurationKey, config);


            IPrincipal originalPrincipal = Thread.CurrentPrincipal;

            HttpResponseMessage responseMsg;
            try
            {
                if (originalPrincipal == null)
                {
                    Thread.CurrentPrincipal = anonymousPrincipal.Value;
                }
                //TODO: consider using await here -- what's the implication of that considering the thread principal may have changed?
                //TODO: This is a candidate for Task.ConfigureAwait(false)
                responseMsg = requestHandler.SendAsync(requestMsg).Result;
            }
            catch
            {
                //TODO: There's a null-reference exception in V4.0 of HttpRoutingDispatcher
                //so if a null reference exception is found -- return a 404

                //TODO: Log this 
                return;
            }
            finally
            {
                //TODO: This thread might be different from the original one. DO we care?
                Thread.CurrentPrincipal = originalPrincipal;
            }

            //Send Response
            try
            {
                //TODO: Why can't the subscriber append the subscriber id itself from within sendresponse
                subscriber.SendResponse(context, CreateResponsePacketFromMessage(responseMsg, subscriber));
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
    }
}
