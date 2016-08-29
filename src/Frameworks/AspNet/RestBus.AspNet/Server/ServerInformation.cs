using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
using System.Collections.Generic;
using RestBus.Common;

namespace RestBus.AspNet.Server
{

    public class ServerInformation
        : IServerInformation
    {

        private List<string> _addresses;

        public IRestBusSubscriber Subscriber { get; internal set; }

        public ICollection<string> Addresses
        {
            get
            {
                return _addresses.AsReadOnly();
            }
        }

        public ServerInformation()
        {
            this._addresses = new List<string>();
        }

        public ServerInformation(IConfiguration configuration)
            : this()
        {
            if (configuration == null)
            {
                throw new ArgumentNullException("configuration");
            }
            this.LoadAddressesFromConfiguration(configuration);
        }

        public void AddAddress(string address)
        {
            _addresses.Add(address);
        }

        protected virtual void LoadAddressesFromConfiguration(IConfiguration configuration)
        {
            //TODO: Figure out how best to incorporate server.urls configuration into addresses
            //Bear in mind that host doesn't have a clue what server/protocol the address is (rabbit, azure etc.)
            //It's informational only -- So it might not be possible for host to tell subscriber what to listen on.
            var urls = configuration["server.urls"];
            if (!string.IsNullOrEmpty(urls))
            {
                this.AddAddress(urls);
            }
        }

    }

}