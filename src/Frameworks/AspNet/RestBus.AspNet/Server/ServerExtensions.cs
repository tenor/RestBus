using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Http.Features;
using RestBus.Common;
using System;

namespace RestBus.AspNet.Server
{
    public static class ServerExtensions
    {
        // TODO: Think of a better name for this -- Is UseRestBusConfig or UseRestBusSetup better?
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
