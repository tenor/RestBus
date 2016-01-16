using System;

namespace RestBus.RabbitMQ.Client
{
    public class ClientSettings
    {
        RestBusClient _client;
        ClientAckBehavior _ackBehavior;
        bool _disableDirectReplies;
        int _prefetchCount;

        public ClientSettings()
        {
            PrefetchCount = AmqpUtils.DEFAULT_PREFETCH_COUNT;
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
                if (value < 0 || value > ushort.MaxValue) throw new ArgumentException("PrefetchCount must be between 0 and 65535.");
                _prefetchCount = value;
            }
        }

        internal RestBusClient Client
        {
            set
            {
                if (_client != null) throw new InvalidOperationException("This instance of ClientSettings is already in use by another client." );
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
