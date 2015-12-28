using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RestBus.RabbitMQ.Subscription
{
    /// <summary>
    /// Encapsulates a RabbitMQ message and its associated consumer.
    /// </summary>
    internal class MessageDispatch
    {
        public IBasicConsumer Consumer;
        public BasicDeliverEventArgs Delivery;
    }
}
