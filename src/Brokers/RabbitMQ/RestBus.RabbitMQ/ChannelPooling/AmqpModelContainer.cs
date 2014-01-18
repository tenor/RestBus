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
        internal AmqpModelContainer(IModel channel, ChannelFlags flags  )
        {
            Channel = channel;
            Flags = flags;
        }

        internal IModel Channel { get; private set; }
        internal ChannelFlags Flags { get; private set; }
    }
}
