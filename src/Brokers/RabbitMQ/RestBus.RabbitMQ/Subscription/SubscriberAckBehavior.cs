namespace RestBus.RabbitMQ.Subscription
{
    public enum SubscriberAckBehavior
    {
        /// <summary>
        /// Requests are explicitly acknowledged after they have been fully processed.
        /// </summary>
        ProcessedRequests,

        /// <summary>
        /// Requests are automatically acknowledged by the broker once they are received by the subscriber.
        /// </summary>
        Automatic
    }
}
