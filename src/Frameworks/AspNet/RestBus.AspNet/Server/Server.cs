using System;
using System.Collections.Generic;
using Microsoft.AspNet.Hosting;
using Microsoft.AspNet.Hosting.Server;
using Microsoft.AspNet.Http.Features;
using Microsoft.Extensions.Logging;

namespace RestBus.AspNet.Server
{
    public class Server : IServer
    {
        private Stack<IDisposable> _disposables;
        private readonly IApplicationLifetime _applicationLifetime;
        private readonly ILogger _logger;

        public Server(IFeatureCollection features, IApplicationLifetime applicationLifetime, ILogger logger)
        {
            if (features == null)
            {
                throw new ArgumentNullException(nameof(features));
            }

            if (applicationLifetime == null)
            {
                throw new ArgumentNullException(nameof(applicationLifetime));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            _applicationLifetime = applicationLifetime;
            _logger = logger;
            Features = features;
        }

        public IFeatureCollection Features { get; }

        public void Start<TContext>(IHttpApplication<TContext> application)
        {
            //TODO: Code a better way to prevent same server from starting twice.
            if (_disposables != null)
            {
                // The server has already started and/or has not been cleaned up yet
                throw new InvalidOperationException("Server has already started.");
            }
            _disposables = new Stack<IDisposable>();

            try
            {
                var information = (ServerInformation)Features.Get<IServerInformation>();

                if(information.Subscriber == null)
                {
                    throw new InvalidOperationException($"RestBus subscriber could not be found. To use the RestBus server, call app.{nameof(ServerExtensions.ConfigureRestBusServer)} in Startup.Configure method and specify a subscriber to use!");
                }

                var host = new RestBusHost<TContext>(information.Subscriber, application);
                _disposables.Push(host);
                host.Start();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            if (_disposables != null)
            {
                while (_disposables.Count > 0)
                {
                    _disposables.Pop().Dispose();
                }
                _disposables = null;
            }
        }
    }
}
