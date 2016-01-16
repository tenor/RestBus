using System;

namespace RestBus.RabbitMQ.Subscription
{
    public class SubscriberSettings
    {
        RestBusSubscriber _subscriber;
        SubscriberAckBehavior _ackBehavior;
        int _prefetchCount;

        public SubscriberSettings()
        {
            PrefetchCount = AmqpUtils.DEFAULT_PREFETCH_COUNT;
        }

        internal SubscriberSettings(RestBusSubscriber subscriber)
        {
            this._subscriber = subscriber;
        }

        public SubscriberAckBehavior AckBehavior
        {
            get
            {
                return _ackBehavior;
            }
            set
            {
                EnsureNotStarted();
                _ackBehavior = value;
            }
        }

        public int PrefetchCount
        {
            get { return _prefetchCount; }
            set
            {
                EnsureNotStarted();
                if (value < 0 || value > ushort.MaxValue) throw new ArgumentException("PrefetchCount must be between 0 and 65535.");
                _prefetchCount = value;
            }
        }


        internal RestBusSubscriber Subscriber
        {
            set
            {
                _subscriber = value;
            }
        }

        private void EnsureNotStarted()
        {
            if (_subscriber != null && _subscriber.HasStarted)
            {
                throw new InvalidOperationException("This instance has already started. Properties can only be modified before starting the subscriber.");
            }
        }

        //TODO: Add other settings
        //AMQP Headers for Headers exchange subscription.
        //AMQP Topic for Topic Exchange subscription.
        //Throttling option -- Lets subscriber throttle how many messages can be processed at once (PrefetchCount kind of takes care of this)
        //RequestedHeartBeat (use in coordination with RequestedHeartBeat query string in connection URI)
    }
}
