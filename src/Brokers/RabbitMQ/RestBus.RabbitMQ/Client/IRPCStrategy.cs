using RabbitMQ.Client.Framing;
using RestBus.RabbitMQ.ChannelPooling;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace RestBus.RabbitMQ.Client
{
    internal interface IRPCStrategy : IDisposable
    {
        void EnsureConnected(bool requestExpectsResponse);

        ExpectedResponse PrepareForResponse(string correlationId, BasicProperties basicProperties, AmqpModelContainer model, HttpRequestMessage request, TimeSpan requestTimeout, TaskCompletionSource<HttpResponseMessage> taskSource);

        AmqpModelContainer GetModel(bool streamsPublisherConfirms);

        void CleanupMessagingResources(string correlationId, ExpectedResponse expectedResponse);
    }
}
