using RestBus.Common.Amqp;
using RestBus.RabbitMQ;
using System.Linq;

namespace Examples
{
    /// <summary>
    /// Represents a message mapper that queues messages persistently.
    /// </summary>
    class QueueingMessageMapper : BasicMessageMapper
    {
        public QueueingMessageMapper(string amqpHostUri, string serviceName) :base(amqpHostUri, serviceName)
        {
        }

        public override ExchangeConfiguration GetExchangeConfig()
        {
            //Returns an ExchangeConfig set up to have persistent queues and messages.

            var connectionInfos = base.amqpHostUris.Select(u => new AmqpConnectionInfo { Uri = u, FriendlyName = base.StripUserInfoAndQuery(u) }).ToArray();
            return new ExchangeConfiguration(connectionInfos, base.serviceName)
            {
                PersistentMessages = true,
                PersistentWorkQueuesAndExchanges = true,
                MessageExpires = (m) => { return false; }, //Messages never expire
                MessageExpectsReply = (m) => { return false; } //Messages are not replied to
            };
        }

    }
}
