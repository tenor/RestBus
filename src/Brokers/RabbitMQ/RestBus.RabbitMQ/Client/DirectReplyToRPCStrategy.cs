using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client.Framing;
using RestBus.RabbitMQ.ChannelPooling;
using RestBus.Common.Amqp;

namespace RestBus.RabbitMQ.Client
{
    internal class DirectReplyToRPCStrategy : IRPCStrategy
    {
        readonly ConnectionManager connectionMgr;
        readonly object reconnectionSync = new object();


        public DirectReplyToRPCStrategy(ClientSettings clientSettings, ExchangeConfiguration exchangeConfig)
        {
            connectionMgr = new ConnectionManager(exchangeConfig);
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
            model.StartConsuming();
            return model;
        }

        public ExpectedResponse PrepareForResponse(string correlationId, BasicProperties basicProperties, AmqpModelContainer model, HttpRequestMessage request, TimeSpan requestTimeout, TaskCompletionSource<HttpResponseMessage> taskSource)
        {
            //Set Reply to queue
            basicProperties.ReplyTo = RPCStrategyHelpers.DIRECT_REPLY_TO_QUEUENAME_ARG;
            var rpcModel = (RPCModelContainer)model;
            var arrival = new ExpectedResponse(rpcModel.ReceivedResponseEvent);
            rpcModel.ExpectResponse(correlationId, arrival);

            RPCStrategyHelpers.WaitForResponse(request, arrival, requestTimeout, taskSource, () => CleanupMessagingResources(correlationId, arrival));
            return arrival;
        }

        public void Dispose()
        {
            var pool = connectionMgr.GetPool();
            if (pool != null) pool.Dispose();
        }
    }
}
