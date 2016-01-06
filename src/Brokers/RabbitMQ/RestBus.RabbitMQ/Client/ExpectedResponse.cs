using RestBus.Common;
using System;
using System.Threading;

namespace RestBus.RabbitMQ.Client
{
    internal class ExpectedResponse
    {
        internal HttpResponsePacket Response;
        internal Exception DeserializationException;
        internal readonly ManualResetEventSlim ReceivedEvent;

        internal ExpectedResponse()
        {
            ReceivedEvent = new ManualResetEventSlim();
        }
    }
}
