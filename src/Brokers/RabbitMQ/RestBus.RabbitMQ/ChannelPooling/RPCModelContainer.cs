using RabbitMQ.Client;
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
        ConcurrentQueueingConsumer queue;
        ManualResetEventSlim receivedResponse;
        string expectedCorrelationId;

        internal RPCModelContainer(IModel channel, 
            ManualResetEventSlim receivedResponse, 
            bool streamsPublisherConfirms, 
            AmqpChannelPooler source ) : base(channel, ChannelFlags.RPC, source )
        {
            this.receivedResponse = receivedResponse;
            queue = new ConcurrentQueueingConsumer(channel, receivedResponse);
        }

        internal bool IsExpectingResponse
        {
            get
            {
                return expectedCorrelationId != null;
            }
        }

        internal void ExpectResponse(string correlationId)
        {
            expectedCorrelationId = correlationId;
        }

        internal void Reset()
        {
            expectedCorrelationId = null;
        }

        internal override void Destroy()
        {
            receivedResponse.Dispose();
            base.Destroy();
        }
    }
}
