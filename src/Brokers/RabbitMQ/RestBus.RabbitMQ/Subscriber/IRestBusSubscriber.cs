using RestBus.RabbitMQ.Common;
using System;
namespace RestBus.RabbitMQ.Subscriber
{
    public interface IRestBusSubscriber : IDisposable
    {
        string Id { get; }
        HttpContext Dequeue();
        void Start();

        //TODO: Remove Restart once restart is called from within Dequeue()
        void Restart();

        //TODO: This interface shouldn't be aware of the HttpContext type which is a RabbitMQ type
        void SendResponse(HttpContext context, HttpResponsePacket response);
    }
}
