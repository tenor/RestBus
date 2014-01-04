
namespace RestBus.Common.Amqp
{
    public class ExchangeInfo
    {
        public ExchangeInfo(string serverAddress, string exchange, string exchangeType)
        {
            //TODO: Check for invalid parameters

            this.ServerAddress = serverAddress;
            this.Exchange = exchange;
            this.ExchangeType = exchangeType;

        }

        public string ServerAddress { get; protected set; }
        public string Exchange { get; protected set; }
        public string ExchangeType { get; protected set; }
    }
}
