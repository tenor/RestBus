using System;

namespace RestBus.Common.Amqp
{
    //TODO: Describe what this class does.
    public class ExchangeInfo
    {
        public ExchangeInfo(string serverAddress, string name, ExchangeKind supportedKinds)
        {
            //TODO: Check for invalid parameters

            this.ServerAddress = serverAddress;
            this.Name = NormalizeExchangeName(name);
            this.SupportedKinds = supportedKinds;

        }

        public string ServerAddress { get; protected set; }
        public string Name { get; protected set; }
        public ExchangeKind SupportedKinds { get; protected set; }

        private static string NormalizeExchangeName(string name)
        {
            //TODO: Have a static IsValidExchangeOrQueueName that this method checks, the BasicMessageMapper will check that too for servicenames.

            if (String.IsNullOrWhiteSpace(name))
            {
                name = "/";
            }

            name = name.Trim();
            if (!name.StartsWith("/"))
            {
                name = "/" + name;
            }
            if (!name.EndsWith("/"))
            {
                name = name + "/";
            }

            return name;
        }
    }
}
