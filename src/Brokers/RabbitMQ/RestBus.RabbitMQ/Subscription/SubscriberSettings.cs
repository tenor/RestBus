using System;

namespace RestBus.RabbitMQ.Subscription
{
    public class SubscriberSettings
    {
        RestBusSubscriber _subscriber;
        SubscriberAckBehavior _ackBehavior;

        public SubscriberSettings()
        {
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
    }
}
