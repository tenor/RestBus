using System;

namespace RestBus.RabbitMQ.Client
{
    public class ClientSettings
    {
        RestBusClient _client;
        ClientAckBehavior _ackBehavior;
        bool _disableDirectReplies;
        int _prefetchCount;

        const int DEFAULT_PREFETCH_COUNT = 50; //Based on measurements from http://www.rabbitmq.com/blog/2012/04/25/rabbitmq-performance-measurements-part-2/

        public ClientSettings()
        {
            PrefetchCount = DEFAULT_PREFETCH_COUNT;
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

        public int PrefetchCount
        {
            get { return _prefetchCount; }
            set
            {
                EnsureNotStartedOrDisposed();
                if (value < 0) throw new ArgumentException("Consumer prefetch must be a positive number or zero.");
                _prefetchCount = value;
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

        //TODO: Add the following settings
        //ExchangeType (for using fanout,headers ...)
        //RequestedHeartBeat (use in coordination with RequestedHeartBeat query string in connection URI)
    }
}
