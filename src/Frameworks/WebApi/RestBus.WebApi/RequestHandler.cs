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


        internal RequestHandler(HttpConfiguration config)
        {
            this.config = config;
            this.dispatcher = new HttpRoutingDispatcher(config);

        }

        internal Task<HttpResponseMessage> SendMessageAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return base.SendAsync(request, cancellationToken);
        }

        internal void EnsureInitialized()
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
            InnerHandler = GetSafePipeline(dispatcher, config.MessageHandlers);
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

        private HttpMessageHandler GetSafePipeline(HttpMessageHandler innerHandler, System.Collections.ObjectModel.Collection<DelegatingHandler> handlers)
        {
            /*
             * This method exists because HttpClientFactory.CreatePipeline will chain the messagehandlers and so, 
             * when HttpServer calls HttpClientFactory.CreatePipeline, it will throw an exception if it has already been chained. 
             * i.e.:
             * 
             * If you make a request through RestBus before making any request through regular HTTP, regular HTTP requests to
             * WebAPI will fail.
             * 
             * See http://aspnetwebstack.codeplex.com/workitem/260 for a related issue that triggers this bug
             * 
             * The most robust way to fix this is to detect that the pipeline has not been chained and then create 
             * some kind of MessagehandlerWrapper pipeline that's chained and use that instead without chaining 
             * the individual MessageHandlers it wraps.
             * 
             * For now, just return the first message handler if it's already chained, if not return the innerHandler.
             * 
             */

            if (handlers == null || !System.Linq.Enumerable.Any(handlers))
            {
                return innerHandler;
            }

            var first = System.Linq.Enumerable.First(handlers);
            if (first.InnerHandler != null)
            {
                //Already wired
                return first;
            }

            return innerHandler;


        }

    }
}
