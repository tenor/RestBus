using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using System.Threading;
using RestBus.Common;

namespace RestBus.AspNet.Server
{

    public class Server
        : IServer
    {

        internal const string ConfigServerArgumentName = "server"; // The argument passed to Microsoft.AspNetCore..Hosting.Program.Main()
        internal const string ConfigServerAssembly = "RestBus.AspNet"; // The server assembly name passed to Microsoft.AspNetCore..Hosting.Program.Main()

        internal static int InstanceCount;

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

            Interlocked.Add(ref InstanceCount, 1);
        }

        public void Start<TContext>(IHttpApplication<TContext> application)
        {
            this.Start(application, null);
        }

        public void Start<TContext>(IHttpApplication<TContext> application, IRestBusSubscriber subscriber)
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
                var information = this.Features.Get<IServerInformation>();

                if (information?.Subscriber == null)
                {
                    if (subscriber != null)
                    {
                        information = new ServerInformation()
                        {
                            Subscriber = subscriber
                        };
                        //information.Subscriber = subscriber;
                    }
                    else
                    {
                        throw new InvalidOperationException($"RestBus subscriber could not be found. To use the RestBus server, call app.{nameof(ServerExtensions.ConfigureRestBusServer)} in Startup.Configure method and specify a subscriber to use.");
                    }
                }
                else
                {
                    subscriber = information.Subscriber;
                }

                //TODO: Add _logger properly
                this._logger.LogInformation($"{nameof(Server)} is starting the RestBus host.");
                var host = new RestBusHost<TContext>(subscriber, application, this._applicationLifetime, this._logFactory);

                //Register host for disposal
                //TODO: Make IApplicationLifeTime.Stopping to stop polling the queue.
                this._applicationLifetime.ApplicationStopping.Register(() =>
                {
                    //TODO: Make ApplicationStopping event stop dequeueing items (StopPollingQueue)
                    host.Dispose();
                });
                this._applicationLifetime.ApplicationStopped.Register(() => host.Dispose());

                _disposables.Push(host);

                host.Start();

                foreach (var name in subscriber.ConnectionNames)
                {
                    information.AddAddress(name);
                }
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex.Message + Environment.NewLine + ex.StackTrace);
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