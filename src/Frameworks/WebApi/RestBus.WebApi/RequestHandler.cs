using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Dispatcher;

namespace RestBus.WebApi
{
    internal class RequestHandler : DelegatingHandler
    {
        private readonly HttpMessageHandler dispatcher;
        private readonly HttpConfiguration config;
        private bool disposed = false;
        private bool initialized = false;
        private object initializationSync = new object();
        private object initializationTarget;


        public RequestHandler(HttpConfiguration config)
        {
            this.config = config;
            this.dispatcher = new HttpRoutingDispatcher(config);

        }

        public Task<HttpResponseMessage> SendAsync (HttpRequestMessage request)
        {
            return base.SendAsync(request, CancellationToken.None);
        }

        public void EnsureInitialized()
        {
            //Not sure why this lazy initialization was done this way in aspnetwebstack\HttpServer.cs
            LazyInitializer.EnsureInitialized(ref initializationTarget, ref initialized, ref initializationSync, () =>
            {
                Initialize();
                return null;
            });
        }

        // Prepares the host for operation.
        // This method must be called after all configuration is complete
        // but before the first request is processed.
        void Initialize()
        {
            config.Initializer(config);
            InnerHandler = HttpClientFactory.CreatePipeline(dispatcher, config.MessageHandlers);
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                disposed = true;
                if (disposing)
                {
                    config.Dispose();
                }
            }

            base.Dispose(disposing);
        }

    }
}
