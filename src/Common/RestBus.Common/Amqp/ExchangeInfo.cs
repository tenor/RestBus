using System;

namespace RestBus.Common.Amqp
{
    //TODO: Describe what this class does.
    public class ExchangeInfo
    {
        //TODO: exchangeType should be an enum.
        public ExchangeInfo(string serverAddress, string name, string kind)
        {
            //TODO: Check for invalid parameters

            this.ServerAddress = serverAddress;
            this.Name = NormalizeExchangeName(name);
            this.Kind = kind;

        }

        public string ServerAddress { get; protected set; }
        public string Name { get; protected set; }
        public string Kind { get; protected set; }

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
