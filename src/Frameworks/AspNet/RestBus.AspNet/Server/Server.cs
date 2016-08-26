using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;

namespace RestBus.AspNet.Server
{

    public class Server
        : IServer
    {

        internal const string ConfigServerArgumentName = "server"; // The argument passed to Microsoft.AspNetCore..Hosting.Program.Main()
        internal const string ConfigServerAssembly = "RestBus.AspNet"; // The server assembly name passed to Microsoft.AspNetCore..Hosting.Program.Main()

        private readonly IApplicationLifetime _applicationLifetime;
        private readonly ILogger _logger;
        private readonly ILoggerFactory _logFactory;

        private Stack<IDisposable> _disposables;

        public IFeatureCollection Features { get; }

        public Server(RestBusFeatureCollection features, IApplicationLifetime applicationLifetime, ILoggerFactory logFactory)
        {
            if (features == null)
            {
                throw new ArgumentNullException(nameof(features));
            }

            if (applicationLifetime == null)
            {
                throw new ArgumentNullException(nameof(applicationLifetime));
            }

            if (logFactory == null)
            {
                throw new ArgumentNullException(nameof(logFactory));
            }

            this._logFactory = logFactory;
            this._applicationLifetime = applicationLifetime;
            this._logger = logFactory.CreateLogger<Server>();

            this.Features = features;
        }

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

                if (information.Subscriber == null)
                {
                    throw new InvalidOperationException($"RestBus subscriber could not be found. To use the RestBus server, call app.{nameof(ServerExtensions.ConfigureRestBusServer)} in Startup.Configure method and specify a subscriber to use.");
                }

                //TODO: Add _logger properly

                this._logger.LogInformation($"{nameof(Server)} is starting the RestBus host.");
                var host = new RestBusHost<TContext>(information.Subscriber, application, this._applicationLifetime, this._logFactory);
                _disposables.Push(host);

                //TODO: Make IApplicationLifeTime.Stopping to stop polling the queue.

                host.Start();

                foreach (var name in information.Subscriber.ConnectionNames)
                {
                    information.AddAddress(name);
                }
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