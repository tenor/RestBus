using RestBus.RabbitMQ.Common;
using System;
namespace RestBus.RabbitMQ.Subscriber
{
    public interface IRestBusSubscriber : IDisposable
    {
        HttpContext Dequeue();
        void Start();
        void Restart();

        //TODO: This interface shouldn't be aware of the HttpContext type which is a RabbitMQ type
        void SendResponse(HttpContext context, HttpResponsePacket response);
    }
}
