using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Http.Features;
using RestBus.Common;
using System;

namespace RestBus.AspNet.Server
{
    public static class ServerExtensions
    {
        // TODO: Think of a better name for this -- Is UseRestBusConfig or UseRestBusSetup better?

        /// <summary>
        /// Configures the RestBus server to use a specified subscriber
        /// </summary>
        /// <param name="app">The Application builder</param>
        /// <param name="subscriber">The RestBus subscriber</param>
        public static void UseRestBusConfiguration( this IApplicationBuilder app, IRestBusSubscriber subscriber )
        {
            if (app == null) throw new ArgumentNullException("app");
            if (subscriber == null) throw new ArgumentNullException("subscriber");

            var feature = app.ServerFeatures.Get<IServerInformation>();
            if (feature == null) return; //Application isn't running RestBus server so return

            var serverInfo = feature as ServerInformation;
            if(serverInfo != null)
            {
                serverInfo.Subscriber = subscriber;
            }
        }
    }
}
