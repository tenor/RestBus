using System;
using Microsoft.AspNet.Hosting;
using Microsoft.AspNet.Hosting.Server;
using Microsoft.AspNet.Http.Features;
using Microsoft.AspNet.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace RestBus.AspNet.Server
{
    //TODO: Describe what this class does
    public class ServerFactory : IServerFactory
    {
        private readonly IApplicationLifetime _appLifetime;
        private readonly ILoggerFactory _loggerFactory;

        public ServerFactory(IApplicationLifetime appLifetime, ILoggerFactory loggerFactory)
        {
            _appLifetime = appLifetime;
            _loggerFactory = loggerFactory;
        }

        public IServer CreateServer(IConfiguration configuration)
        {
            var information = new ServerInformation(configuration);
            var serverFeatures = new FeatureCollection();
            serverFeatures.Set<IServerInformation>(information);
            serverFeatures.Set<IServerAddressesFeature>(information);
            return new Server(serverFeatures, _appLifetime, _loggerFactory.CreateLogger(Server.ConfigServerAssembly));
        }
    }
}
