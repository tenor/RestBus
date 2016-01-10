using RabbitMQ.Client.Framing;
using RestBus.Common.Amqp;
using RestBus.RabbitMQ.ChannelPooling;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace RestBus.RabbitMQ.Client
{
    internal class DirectReplyToRPCStrategy : IRPCStrategy
    {
        readonly ConnectionManager connectionMgr;
        readonly object reconnectionSync = new object();

        public bool ReturnModelAfterSending
        {
            get
            {
                //Don't return model after sending message, to prevent another SendAsync from using the consumer while it's waiting for a response
                //Model will be returned once the response is received or times out or cancelled.
                return false;
            }
        }

        public DirectReplyToRPCStrategy(ClientSettings clientSettings, ConnectionManager connectionManager)
        {
            this.connectionMgr = connectionManager;
        }

        public void CleanupMessagingResources(string correlationId, ExpectedResponse expectedResponse)
        {
            if (expectedResponse != null) expectedResponse.Dispose();
        }

        public void EnsureConnected(bool requestExpectsResponse)
        {
            //Client has never seen any request expecting a response
            //so just try to connect if not already connected
            connectionMgr.ConnectIfUnconnected(reconnectionSync);

            //TODO: This call is unnecessary if we can guarantee the block above always creates a connection and pool.
            //Consider runningonly in debug mode.
            connectionMgr.EnsurePoolIsCreated();
        }

        public AmqpModelContainer GetModel(bool streamsPublisherConfirms)
        {
            var pooler = connectionMgr.GetPool();
            var model = (RPCModelContainer) pooler.GetModel(streamsPublisherConfirms ? ChannelFlags.RPCWithPublisherConfirms : ChannelFlags.RPC );
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
            var pool = connectionMgr.GetPool();
            if (pool != null) pool.Dispose();
        }
    }
}
