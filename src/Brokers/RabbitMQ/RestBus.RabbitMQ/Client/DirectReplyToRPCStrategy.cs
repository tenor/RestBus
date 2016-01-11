using RabbitMQ.Client.Framing;
using RestBus.RabbitMQ.ChannelPooling;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace RestBus.RabbitMQ.Client
{
    internal class DirectReplyToRPCStrategy : IRPCStrategy
    {
        public bool ReturnModelAfterSending
        {
            get
            {
                //Don't return model after sending message, to prevent another SendAsync from using the consumer while it's waiting for a response
                //Model will be returned once the response is received or times out or cancelled.
                return false;
            }
        }

        public void CleanupMessagingResources(string correlationId, ExpectedResponse expectedResponse)
        {
            if (expectedResponse != null) expectedResponse.Dispose();
        }

        public void StartStrategy(AmqpChannelPooler pool, bool requestExpectsResponse)
        {
            //Nothing to start
        }

        public AmqpModelContainer GetModel(AmqpChannelPooler pool, bool streamsPublisherConfirms)
        {
            var model = (RPCModelContainer)pool.GetModel(streamsPublisherConfirms ? ChannelFlags.RPCWithPublisherConfirms : ChannelFlags.RPC );
            model.Reset();
            return model;
        }

        public ExpectedResponse PrepareForResponse(string correlationId, BasicProperties basicProperties, AmqpModelContainer model, HttpRequestMessage request, TimeSpan requestTimeout, CancellationToken cancellationToken, TaskCompletionSource<HttpResponseMessage> taskSource)
        {
            //Set Reply to queue
            basicProperties.ReplyTo = RPCStrategyHelpers.DIRECT_REPLY_TO_QUEUENAME_ARG;
            var rpcModel = (RPCModelContainer)model;
            var arrival = new ExpectedResponse(rpcModel.ReceivedResponseEvent);
            rpcModel.ExpectResponse(correlationId, arrival);

            RPCStrategyHelpers.WaitForResponse(request, arrival, requestTimeout, model, true, cancellationToken, taskSource, () => CleanupMessagingResources(correlationId, arrival));
            return arrival;
        }

        public void Dispose()
        {
            //Nothing to dispose
        }
    }
}
