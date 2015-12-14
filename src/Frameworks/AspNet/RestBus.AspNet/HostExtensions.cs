using Microsoft.AspNet.Builder;
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
            host.Start();

            //TODO: Add host Disposal code
        }

    }
}
