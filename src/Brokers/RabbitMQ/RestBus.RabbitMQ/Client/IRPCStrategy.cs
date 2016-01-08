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

        void PrepareForResponse(string correlationId, ExpectedResponse arrival, BasicProperties basicProperties, HttpRequestMessage request, TimeSpan requestTimeout, TaskCompletionSource<HttpResponseMessage> taskSource);

        AmqpModelContainer GetModel();

        void CleanupMessagingResources(string correlationId, ExpectedResponse expectedResponse);
    }
}
