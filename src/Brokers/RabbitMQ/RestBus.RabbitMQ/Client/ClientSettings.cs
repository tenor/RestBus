namespace RestBus.RabbitMQ.Client
{
    public class ClientSettings
    {
        public ClientAckBehavior AckBehavior { get; set; }
        public bool DisableDirectReplies { get; set; }
    }
}
