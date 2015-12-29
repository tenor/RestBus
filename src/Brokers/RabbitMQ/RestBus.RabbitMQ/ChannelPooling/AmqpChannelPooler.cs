using RabbitMQ.Client;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace RestBus.RabbitMQ.ChannelPooling
{
    internal sealed class AmqpChannelPooler : IDisposable
    {
        readonly IConnection conn;
        volatile bool _disposed;

#if !DISABLE_CHANNELPOOLING

        readonly ConcurrentDictionary<ChannelFlags, ConcurrentQueue<AmqpModelContainer>> _pool = new ConcurrentDictionary<ChannelFlags, ConcurrentQueue<AmqpModelContainer>>();
        static readonly int MODEL_EXPIRY_TIMESPAN = (int)TimeSpan.FromMinutes(1).TotalMilliseconds;

#endif

        public bool IsDisposed
        {
            get { return _disposed; }
        }

        public bool IsDirectReplyToCapable { get; }

        public AmqpChannelPooler(IConnection conn)
        {
            this.conn = conn;
            IsDirectReplyToCapable = DetermineDirectReplyToCapability(conn);
        }

        internal AmqpModelContainer GetModel(ChannelFlags flags)
        {
#if !DISABLE_CHANNELPOOLING

            if(flags == ChannelFlags.Consumer)
            {
                //Do not pool consumer related channels
                return CreateModel(flags);
            }

            //Search pool for a model:
            AmqpModelContainer model = null;

            //First get the queue
            ConcurrentQueue<AmqpModelContainer> queue;
            if (_pool.TryGetValue(flags, out queue))
            {
                bool retry;
                int tick = Environment.TickCount;

                //Dequeue queue until an unexpired model is found 
                do
                {
                    retry = false;
                    model = null;
                    if (queue.TryDequeue(out model))
                    {
                        if (HasModelExpired(tick, model))
                        {

                            DisposeModel(model); // dispose model
                            retry = true;
                        }
                    }
                }
                while (retry );
            }

            if (model == null)
            {
                //Wasn't found, so create a new one
                model = CreateModel(flags);
            }

            return model;


#else
            return CreateModel(flags);
#endif

        }

        internal void ReturnModel(AmqpModelContainer modelContainer)
        {
            // NOTE: do not call AmqpModelContainer.Close() here.
            // That method calls this method.

#if !DISABLE_CHANNELPOOLING

            //Do not return channel to pool if either:
            //1. Pooler is disposed
            //2. Channel is consumer related
            //3. Channel has expired
            if (_disposed || modelContainer.Flags == ChannelFlags.Consumer || HasModelExpired(Environment.TickCount, modelContainer))
            {
                DisposeModel(modelContainer);
                return;
            }

            //Insert model in pool
            ConcurrentQueue<AmqpModelContainer> queue;
            if (_pool.TryGetValue(modelContainer.Flags, out queue))
            {
                //Found the queue so just enqueue the model
                queue.Enqueue(modelContainer);

                //TODO: AddOrUpdate below performs this lookup so this code here is redundant.
                //Consider removing this code and using an Add factory to eliminate the new queue allocation below
            }
            else
            {
                //Attempt to add a new queue, if a queue doesn't exist and if it does, then add model to queue

                queue = new ConcurrentQueue<AmqpModelContainer>();
                queue.Enqueue(modelContainer);

                _pool.AddOrUpdate(modelContainer.Flags, queue, (f, q) => { q.Enqueue(modelContainer); return q; });
            }


            //It's possible for the disposed flag to be set (and the pool flushed) right after the first _disposed check
            //but before the modelContainer was added, so check again. 
            if (_disposed)
            {
                Flush();
            }

#else
            DisposeModel(modelContainer);
#endif
        }



        public void Dispose()
        {
            _disposed = true;

#if !DISABLE_CHANNELPOOLING
            Flush();
#endif
            

        }

#if !DISABLE_CHANNELPOOLING
        private void Flush()
        {
            var snapshot = _pool.ToArray();

            ConcurrentQueue<AmqpModelContainer> queue;
            AmqpModelContainer model;
            foreach (var kv in snapshot)
            {
                queue = kv.Value;
                while (queue.TryDequeue(out model))
                {
                    DisposeModel(model);
                }
            }
        }


        private static bool HasModelExpired(int currentTickCount, AmqpModelContainer modelContainer)
        {
            //TickCount wrapped around (so timespan can't be trusted) or model has expired
            return currentTickCount < modelContainer.Created || modelContainer.Created < (currentTickCount - MODEL_EXPIRY_TIMESPAN);
        }

#endif

        private AmqpModelContainer CreateModel(ChannelFlags flags)
        {
            return new AmqpModelContainer(conn.CreateModel(), flags, this);
        }

        private static void DisposeModel(AmqpModelContainer modelContainer)
        {
            if (modelContainer != null && modelContainer.Channel != null)
            {
                try
                {
                    modelContainer.Channel.Dispose();
                }
                catch { }
            }
        }

        /// <summary>
        /// Determines if a server has direct reply-to capability. 
        /// </summary>
        private static bool DetermineDirectReplyToCapability(IConnection conn)
        {                        
            if (conn == null) throw new ArgumentNullException("conn");

            //Check for the 'direct_reply_to' capability as introduced by https://github.com/rabbitmq/rabbitmq-server/issues/520

            if (conn.ServerProperties.ContainsKey("capabilities"))
            {
                var capabilities = conn.ServerProperties["capabilities"] as Dictionary<string, object>;
                if(capabilities != null && capabilities.ContainsKey("direct_reply_to"))
                {
                    var directReplyTo = capabilities["direct_reply_to"];
                    if (directReplyTo is bool && (bool)directReplyTo)
                    {
                        return true;
                    }
                }
            }

            //If the capability is not found then parse the version number and check if it's greater than 3.4.0.

            //TODO: In the distant future, say 8 years from now (2023) when v3.4 to v.3.6 are really old versions, you can safely 
            //take out the fallback method below.
            try
            {
                if (conn.ServerProperties.ContainsKey("version"))
                {
                    var versionBytes = conn.ServerProperties["version"] as byte[];
                    if (versionBytes != null && versionBytes.Length > 3)
                    {
                        var version = System.Text.Encoding.ASCII.GetString(versionBytes);
                        var semver = version.Split('.');

                        if (semver.Length > 2 && Int32.Parse(semver[0]) >= 3 && Int32.Parse(semver[1]) >= 4)
                        {
                            //Version is greater or equal to than 3.4.x
                            return true;
                        }
                    }
                }
            }
            catch { }

            return false;
        }
    }
}
