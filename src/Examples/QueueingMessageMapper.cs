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

        public override ExchangeInfo GetExchangeInfo()
        {
            //Return an ExchangeInfo set up to have persistent queues and messages.
            return new ExchangeInfo(base.amqpHostUri, base.serviceName)
            {
                PersistentMessages = true,
                PersistentWorkQueuesAndExchanges = true,
            };
        }

        public override bool GetExpires(HttpRequestMessage request)
        {
            //Messages never expire
            return false;
        }

    }
}
