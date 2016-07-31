using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace RestBus.AspNet.Server
{
    //TODO: Describe what this class does
    //public class ServerFactory : IServerFactory
    //{
    //    private readonly IApplicationLifetime _appLifetime;
    //    private readonly ILoggerFactory _loggerFactory;

    //    public ServerFactory(IApplicationLifetime appLifetime, ILoggerFactory loggerFactory)
    //    {
    //        _appLifetime = appLifetime;
    //        _loggerFactory = loggerFactory;
    //    }

    //    public IServer CreateServer(IConfiguration configuration)
    //    {
    //        var information = new ServerInformation(configuration);
    //        var serverFeatures = new FeatureCollection();
    //        serverFeatures.Set<IServerInformation>(information);
    //        serverFeatures.Set<IServerAddressesFeature>(information);
    //        return new Server(serverFeatures, _appLifetime, _loggerFactory.CreateLogger(Server.ConfigServerAssembly));
    //    }
    //}
}
