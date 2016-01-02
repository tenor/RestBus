using RestBus.Common.Amqp;
using RestBus.RabbitMQ;
using System.Net.Http;

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
            //Return an ExchangeConfig set up to have persistent queues and messages.
            return new ExchangeConfiguration(new string[] { base.amqpHostUri }, base.serviceName)
            {
                PersistentMessages = true,
                PersistentWorkQueuesAndExchanges = true,
                MessageExpires = (m) => { return false; } //Messages never expire
            };
        }

    }
}
