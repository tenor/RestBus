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
        //TODO: Is 'RunRestBusHost' a better name? For: It's not technically middleware, and it's supposed to be called last in the pipeline. Against: The app 'uses' the host.
        //TODO: Test when UseRestBusHost is not the last call for the pipeline.

        /// <summary>
        /// Starts a new <see cref="RestBusHost{TContext}"/> using the specified subscriber.
        /// </summary>
        /// <param name="app">The Application builder</param>
        /// <param name="subscriber">The RestBus subscriber</param>
        public static void UseRestBusHost(this IApplicationBuilder app, IRestBusSubscriber subscriber)
        {
            if (app == null) throw new ArgumentNullException("app");
            if (subscriber == null) throw new ArgumentNullException("subscriber");

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
