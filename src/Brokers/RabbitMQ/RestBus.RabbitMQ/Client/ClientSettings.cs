namespace RestBus.RabbitMQ.Client
{
    public class ClientSettings
    {
        RestBusClient _client;
        ClientAckBehavior _ackBehavior;
        bool _disableDirectReplies;

        public ClientSettings()
        {
        }

        internal ClientSettings(RestBusClient client)
        {
            this._client = client;
        }

        public ClientAckBehavior AckBehavior
        {
            get { return _ackBehavior; }
            set
            {
                EnsureNotStartedOrDisposed();
                _ackBehavior = value;
            }
        }

        public bool DisableDirectReplies
        {
            get { return _disableDirectReplies; }
            set
            {
                EnsureNotStartedOrDisposed();
                _disableDirectReplies = value;
            }
        }

        internal RestBusClient Client
        {
            set
            {
                _client = value;
            }
        }

        private void EnsureNotStartedOrDisposed()
        {
            if (_client != null)
            {
                _client.EnsureNotStartedOrDisposed();
            }
        }
    }
}
