using RestBus.Common;
using System;
using System.Threading;

namespace RestBus.RabbitMQ.Client
{
    internal class ExpectedResponse : IDisposable
    {
        internal HttpResponsePacket Response;
        internal Exception DeserializationException;
        internal readonly ManualResetEventSlim ReceivedEvent;
        readonly bool _ownsReceivedEvent;
        private ManualResetEventSlim receivedResponseEvent;

        internal ExpectedResponse()
        {
            ReceivedEvent = new ManualResetEventSlim();
            _ownsReceivedEvent = true;
        }

        public ExpectedResponse(ManualResetEventSlim receivedResponseEvent)
        {
            ReceivedEvent = receivedResponseEvent;
        }

        public void Dispose()
        {

            if (ReceivedEvent != null && _ownsReceivedEvent)
            {
                ReceivedEvent.Dispose();
            }
        }
    }
}
