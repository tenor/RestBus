using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RestBus.RabbitMQ.Client;
using RestBus.RabbitMQ.Consumer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RestBus.RabbitMQ.ChannelPooling
{
    internal class RPCModelContainer : AmqpModelContainer
    {
        readonly EventingBasicConsumer _consumer;
        readonly ManualResetEventSlim _receivedResponse;
        readonly object startConsumingSync = new object();
        volatile ExpectedResponse _expected;
        volatile string _correlationId;
        volatile bool _isConsuming;

        internal RPCModelContainer(IModel channel,  
            bool streamsPublisherConfirms, 
            AmqpChannelPooler source ) : base(channel, streamsPublisherConfirms ? ChannelFlags.RPCWithPublisherConfirms : ChannelFlags.RPC, source )
        {
            this._receivedResponse = new ManualResetEventSlim();
            _consumer = new EventingBasicConsumer(channel);
            _consumer.ConsumerCancelled += (s, e) => SetModelToBeDiscarded();
            _consumer.Received += ResponseReceived;
        }

        private void ResponseReceived(object sender, BasicDeliverEventArgs evt)
        {
            var correlationId = _correlationId;
            var expected = _expected;

            if (expected != null && !String.IsNullOrEmpty(evt.BasicProperties.CorrelationId) && evt.BasicProperties.CorrelationId == correlationId)
            {
                RPCStrategyHelpers.ReadAndSignalDelivery(expected, evt);
            }
        }

        private void SetModelToBeDiscarded()
        {
            this.Discard = true; // Consumer was cancelled so destroy this channel when done.
        }

        internal void StartConsuming()
        {
            if(!_isConsuming)
            {
                lock(startConsumingSync)
                {
                    if (_isConsuming) return;
                    this.Channel.BasicConsume(RPCStrategyHelpers.DIRECT_REPLY_TO_QUEUENAME_ARG, true, _consumer);
                    _isConsuming = true;
                }
            }
        }

        internal bool IsExpectingResponse
        {
            get
            {
                return _expected != null;
            }
        }

        public ManualResetEventSlim ReceivedResponseEvent
        {
            get
            {
                return _receivedResponse;
            }
        }

        internal void ExpectResponse(string correlationId,  ExpectedResponse expected)
        {
            _correlationId = correlationId;
            _expected = expected;
        }

        internal void Reset()
        {
            _correlationId = null;
            _expected = null;
            _receivedResponse.Reset();
        }

        internal override void Destroy()
        {
            _receivedResponse.Dispose();
            base.Destroy();
        }
    }
}
