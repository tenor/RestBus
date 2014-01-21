using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RestBus.RabbitMQ.ChannelPooling
{
    internal class AmqpModelContainer
    {
        internal AmqpModelContainer(IModel channel, ChannelFlags flags, AmqpChannelPooler source)
        {
            Channel = channel;
            Flags = flags;
            Source = source;
            Created = Environment.TickCount;
        }

        //This method should only be called from the client and not from any ChannelPooling class, to avoid infinte recursion
        internal void Close()
        {
            Source.ReturnModel(this);
        }

        //TODO: Consider having this property throw an exception if accessed after model has been closed
        internal IModel Channel { get; private set; }
        internal ChannelFlags Flags { get; private set; }
        internal int Created { get; private set; }
        private AmqpChannelPooler Source { get; set; }
    }
}
