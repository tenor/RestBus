using RabbitMQ.Client.Framing;
using RestBus.RabbitMQ.ChannelPooling;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace RestBus.RabbitMQ.Client
{
    internal interface IRPCStrategy : IDisposable
    {
        void StartStrategy(AmqpChannelPooler pool, bool requestExpectsResponse);

        ExpectedResponse PrepareForResponse(string correlationId, BasicProperties basicProperties, AmqpModelContainer model, HttpRequestMessage request, TimeSpan requestTimeout, CancellationToken cancellationToken, TaskCompletionSource<HttpResponseMessage> taskSource);

        AmqpModelContainer GetModel(AmqpChannelPooler pool, bool streamsPublisherConfirms);

        bool ReturnModelAfterSending { get; }

        void CleanupMessagingResources(string correlationId, ExpectedResponse expectedResponse);
    }
}
