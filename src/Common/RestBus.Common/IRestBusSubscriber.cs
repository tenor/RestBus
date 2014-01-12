using System;
namespace RestBus.Common
{
    public interface IRestBusSubscriber : IDisposable
    {
        string Id { get; }
        MessageContext Dequeue();
        void Start();

        void SendResponse(MessageContext context, HttpResponsePacket response);
    }
}
