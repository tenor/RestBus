using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using RestBus.Common;

namespace RestBus.AspNet.Server
{
    internal class ServerInformation : IServerInformation
    {
        List<string> _addresses = new List<string>();

        public ServerInformation(IConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException("configuration");
            }

            //Addresses = GetAddresses(configuration);
        }

        public ICollection<string> Addresses { get { return _addresses.AsReadOnly(); }}

        internal void AddAddress(string address)
        {
            _addresses.Add(address);
        }

        public IRestBusSubscriber Subscriber { get; internal set; }

        /*
        private static ICollection<string> GetAddresses(IConfiguration configuration)
        {
            //TODO: Figure out how best to incorporate server.urls configuration into addresses
            //Bear in mind that host doesn't have a clue what server/protocol the address is (rabbit, azure etc.)
            //It's informational only -- So it might not be possible for host to tell subscriber what to listen on.

            var urls = configuration["server.urls"];

            if (!string.IsNullOrEmpty(urls))
            {
                addresses.Add(urls);
            }

            return _addresses.AsReadOnly();
        }
        */
    }
}
