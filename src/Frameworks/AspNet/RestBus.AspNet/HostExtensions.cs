using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Hosting;
using Microsoft.AspNet.Hosting.Internal;
using Microsoft.AspNet.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RestBus.Common;
using System;
using System.Diagnostics;

namespace RestBus.AspNet
{
    public static class HostExtensions
    {
        //TODO: See if it's possible to prevent middleware from being added after RunRestBusHost() is called, subsequent calls to RunRestBusHost must succeed.

        /// <summary>
        /// Starts running a new RestBus host.
        /// </summary>
        /// <param name="app">The Application builder</param>
        /// <param name="subscriber">The RestBus subscriber</param>
        /// <remarks>The RestBus host will not be started if the application is running a RestBus server.</remarks>
        public static void RunRestBusHost(this IApplicationBuilder app, IRestBusSubscriber subscriber)
        {
            RunRestBusHost(app, subscriber, false);
        }

        /// <summary>
        /// Starts running a new RestBus host.
        /// </summary>
        /// <param name="app">The Application builder</param>
        /// <param name="subscriber">The RestBus subscriber</param>
        /// <param name="continueIfRestBusServer">Set to false to not run the host if the application server is the RestBus.AspNet server</param>
        public static void RunRestBusHost(this IApplicationBuilder app, IRestBusSubscriber subscriber, bool continueIfRestBusServer)
        {
            if (app == null) throw new ArgumentNullException("app");
            if (subscriber == null) throw new ArgumentNullException("subscriber");

            if (!continueIfRestBusServer && 
                app.ApplicationServices.GetRequiredService<IHostingEnvironment>().Configuration[Server.Server.ConfigServerArgumentName] == Server.Server.ConfigServerAssembly)
            {
                //The application is running RestBusServer, so exit
                return;
            }

            var appFunc = app.Build();

            var logger = app.ApplicationServices.GetRequiredService<ILogger<HostingEngine>>();
            var diagnosticSource = app.ApplicationServices.GetRequiredService<DiagnosticSource>();
            var httpContextFactory = app.ApplicationServices.GetRequiredService<IHttpContextFactory>();

            var application = new HostingApplication(appFunc, logger, diagnosticSource, httpContextFactory);

            var host = new RestBusHost<HostingApplication.Context>(subscriber, application);

            //Register host for disposal
            var appLifeTime = app.ApplicationServices.GetRequiredService<IApplicationLifetime>();

            appLifeTime.ApplicationStopping.Register(() => host.Dispose()); //TODO: Make ApplicationStopping event stop dequeueing items (StopPollingQueue)
            appLifeTime.ApplicationStopped.Register(() => host.Dispose());

            //Start host
            host.Start();
        }

    }
}
