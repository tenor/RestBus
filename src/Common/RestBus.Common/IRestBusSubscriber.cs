using System;
namespace RestBus.Common
{
    public interface IRestBusSubscriber : IDisposable
    {
        string Id { get; }
        HttpContext Dequeue();
        void Start();

        void SendResponse(HttpContext context, HttpResponsePacket response);
    }
}
