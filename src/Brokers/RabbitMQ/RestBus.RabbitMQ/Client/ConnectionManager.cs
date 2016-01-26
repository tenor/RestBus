using RabbitMQ.Client;
using RestBus.Common.Amqp;
using RestBus.RabbitMQ.ChannelPooling;
using System;
using System.Collections.Generic;
using System.Threading;

namespace RestBus.RabbitMQ.Client
{
    internal class ConnectionManager : IDisposable
    {
        volatile AmqpChannelPooler _clientPool;
        volatile bool _disposed;
        readonly ConnectionFactory connectionFactory;
        readonly object connectionSync = new object();

        public ConnectionManager(IList<AmqpConnectionInfo> serverUris)
        {
            //Map request to RabbitMQ Host and exchange, 
            this.connectionFactory = new ConnectionFactory();
            connectionFactory.Uri = serverUris[0].Uri;
            connectionFactory.RequestedHeartbeat = RPCStrategyHelpers.HEART_BEAT;
        }

        public AmqpChannelPooler GetConnectedPool()
        {
            return ConnectIfUnconnected();
        }

        private AmqpChannelPooler ConnectIfUnconnected()
        {
            var pool = _clientPool;
            if (pool == null || pool.Connection  == null || !pool.Connection.IsOpen || pool.GetRecycle())
            {
                lock (connectionSync)
                {
                    if (_disposed) throw new ObjectDisposedException(GetType().FullName);

                    if (_clientPool == null || _clientPool.Connection == null || !_clientPool.Connection.IsOpen || _clientPool.GetRecycle())
                    {
                        return CreateNewChannelPool();
                    }
                    else
                    {
                        return _clientPool;
                    }
                }
            }

            return pool;
        }


        private AmqpChannelPooler CreateNewChannelPool()
        {
            //NOTE: This is the only place where connections are created in the client
            //NOTE: CreateConnection() can always throw RabbitMQ.Client.Exceptions.BrokerUnreachableException
            var newConn = connectionFactory.CreateConnection();

            //Swap out client connection and pooler, so other threads can use the new objects:

            //swap out old pool with new pool
            var newPool = new AmqpChannelPooler(newConn);
            var oldpool = Interlocked.Exchange(ref _clientPool, newPool);

            //Dispose old pool
            if (oldpool != null)
            {
                oldpool.Dispose();
            }

            return newPool;
        }

        public void Dispose()
        {
            _disposed = true;
            lock(connectionSync)
            {
                if(_clientPool != null)
                {
                    _clientPool.Dispose();
                }
            }
        }
    }
}
