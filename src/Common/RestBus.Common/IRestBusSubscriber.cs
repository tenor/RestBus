using System;
using System.Collections.Generic;

namespace RestBus.Common
{
    public interface IRestBusSubscriber : IDisposable
    {
        string Id { get; }

        IList<string> ConnectionNames { get; }

        MessageContext Dequeue();

        void Start();

        void SendResponse(MessageContext context, HttpResponsePacket response);
    }
}
