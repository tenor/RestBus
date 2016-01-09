using RabbitMQ.Client;
using RestBus.Common.Amqp;
using RestBus.RabbitMQ.ChannelPooling;
using System;
using System.Threading;

namespace RestBus.RabbitMQ.Client
{
    internal class ConnectionManager
    {
        readonly ConnectionFactory connectionFactory;
        volatile AmqpChannelPooler _clientPool;

        public ConnectionManager(ExchangeConfiguration exchangeConfig)
        {
            //Map request to RabbitMQ Host and exchange, 
            this.connectionFactory = new ConnectionFactory();
            connectionFactory.Uri = exchangeConfig.ServerUris[0].Uri;
            connectionFactory.RequestedHeartbeat = RPCStrategyHelpers.HEART_BEAT;
        }

        public void ConnectIfUnconnected(object syncObj)
        {
            if (_clientPool == null || _clientPool.Connection  == null || !_clientPool.Connection.IsOpen)
            {
                //TODO: Can double-checked locking here be simplified?

                lock (syncObj)
                {
                    if (_clientPool == null || _clientPool.Connection == null || !_clientPool.Connection.IsOpen)
                    {
                        CreateNewChannelPool();
                    }
                }
            }
        }

        public void EnsurePoolIsCreated()
        {
            //Test if conn or pooler is null, then leave
            if (_clientPool == null || _clientPool.Connection == null || _clientPool == null)
            {
                // This means a connection could not be created most likely because the server was Unreachable.
                // This shouldn't happen because StartCallbackQueueConsumer should have thrown the exception

                //TODO: The inner exception here is a good candidate for a RestBusException
                throw RestBusClient.GetWrappedException("Unable to establish a connection.", new ApplicationException("Unable to establish a connection."));
            }
        }

        public AmqpChannelPooler GetPool()
        {
            return _clientPool;
        }

        public AmqpChannelPooler CreateNewChannelPool()
        {
            //NOTE: This is the only place where connections are created in the client
            //NOTE: CreateConnection() can always throw RabbitMQ.Client.Exceptions.BrokerUnreachableException
            var newConn = connectionFactory.CreateConnection();

            //Swap out client connection and pooler, so other threads can use the new objects:

            //wap out old pool with new pool
            var newPool = new AmqpChannelPooler(newConn);
            var oldpool = Interlocked.Exchange(ref _clientPool, newPool);

            //Dispose old pool
            if (oldpool != null)
            {
                oldpool.Dispose();
            }

            return newPool;
        }

        public bool IsConnected
        {
            get
            {
                var pool = _clientPool;
                return pool != null && pool.Connection != null && pool.Connection.IsOpen;
            }
        }
    }
}
