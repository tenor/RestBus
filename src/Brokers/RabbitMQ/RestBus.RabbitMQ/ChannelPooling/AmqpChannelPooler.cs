#undef ENABLE_CHANNELPOOLING

using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;



namespace RestBus.RabbitMQ.ChannelPooling
{

    internal sealed class AmqpChannelPooler : IDisposable
    {
        readonly IConnection conn;
        volatile bool _disposed;

        public bool IsDisposed
        {
            get { return _disposed; }
        }

        public AmqpChannelPooler(IConnection conn)
        {
            this.conn = conn;
        }

        internal AmqpModelContainer GetChannel(ChannelFlags flags)
        {
#if ENABLE_CHANNELPOOLING
            throw new NotImplementedException();

            //TODO: If Disposed, throw disposed exception

#else
            return new AmqpModelContainer( conn.CreateModel(), flags);
#endif

        }

        internal void ReturnChannel(AmqpModelContainer container)
        {
#if ENABLE_CHANNELPOOLING
            throw new NotImplementedException();

            //TODO: If Disposed, Just kill channel

#else
            if (container != null && container.Channel != null)
            {
                try
                {
                    container.Channel.Dispose();
                }
                catch { }
            }
#endif
        }

        public void Dispose()
        {
            _disposed = true;
            
            //TODO: CLear all items in pool
        }
    }
}
