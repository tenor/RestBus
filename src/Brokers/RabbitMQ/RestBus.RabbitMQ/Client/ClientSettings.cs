namespace RestBus.RabbitMQ.Client
{
    public class ClientSettings
    {
        RestBusClient _client;
        ClientAckBehavior _ackBehavior;
        bool _disableDirectReplies;

        public ClientSettings(RestBusClient client)
        {
            this._client = client;
        }

        public ClientAckBehavior AckBehavior
        {
            get { return _ackBehavior; }
            set { _client.EnsureNotStartedOrDisposed(); _ackBehavior = value; }
        }

        public bool DisableDirectReplies
        {
            get { return _disableDirectReplies; }
            set { _client.EnsureNotStartedOrDisposed(); _disableDirectReplies = value; }
        }
    }
}
