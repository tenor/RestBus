using System.Net.Http;
using RestBus.Common.Amqp;
using RestBus.RabbitMQ;

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

        public override MessagingConfiguration MessagingConfig
        {
            get
            {
                //Returns a MessagingConfig set up to have persistent queues and messages.

                return new MessagingConfiguration
                {
                    PersistentMessages = true,
                    PersistentWorkQueuesAndExchanges = true,
                    MessageExpires = (m) => { return false; }, //Messages never expire
                    MessageExpectsReply = (m) => { return false; } //Messages are not replied to
                };
            }
        }
    }
}
