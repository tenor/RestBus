using Microsoft.AspNetCore.Hosting.Server.Features;
using RestBus.Common;

namespace RestBus.AspNet.Server
{

    public interface IServerInformation
        : IServerAddressesFeature
    {

        IRestBusSubscriber Subscriber { get; }

    }

}