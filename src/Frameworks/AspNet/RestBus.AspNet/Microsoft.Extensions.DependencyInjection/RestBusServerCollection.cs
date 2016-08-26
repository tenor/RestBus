using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting.Server.Features;
using RestBus.AspNet.Server;

namespace Microsoft.Extensions.DependencyInjection
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

    // Since IServerFactory was removed in ASP.NET Core 1.0 RTM, we don't need it any more.
    // See https://github.com/aspnet/Hosting/issues/698
    public static class RestBusServerCollection
    {

        public static IServiceCollection AddRestBusServer(this IServiceCollection services, IConfiguration configuration = null)
        {
            var information = new ServerInformation(configuration);
            return AddRestBusServer(services, information);
        }

        public static IServiceCollection AddRestBusServer(this IServiceCollection services, IServerInformation information)
        {
            var serverFeatures = new RestBusFeatureCollection();
            serverFeatures.Set<IServerInformation>(information);
            serverFeatures.Set<IServerAddressesFeature>(information);
            services.AddSingleton(serverFeatures);
            return services;
        }

    }

}
