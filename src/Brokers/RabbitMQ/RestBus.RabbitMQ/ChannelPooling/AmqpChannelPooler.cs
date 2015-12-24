using RabbitMQ.Client;
using System;
using System.Collections.Concurrent;


namespace RestBus.RabbitMQ.ChannelPooling
{

    internal sealed class AmqpChannelPooler : IDisposable
    {
        readonly object syncModelCreate = new object();
        readonly IConnection conn;
        volatile bool _disposed;

#if !DISABLE_CHANNELPOOLING

        readonly ConcurrentDictionary<ChannelFlags, ConcurrentQueue<AmqpModelContainer>> _pool = new ConcurrentDictionary<ChannelFlags, ConcurrentQueue<AmqpModelContainer>>();
        static readonly int MODEL_EXPIRY_TIMESPAN = (int)TimeSpan.FromMinutes(5).TotalMilliseconds;

#endif

        public bool IsDisposed
        {
            get { return _disposed; }
        }

        public AmqpChannelPooler(IConnection conn)
        {
            this.conn = conn;
        }

        internal AmqpModelContainer GetModel(ChannelFlags flags)
        {
#if !DISABLE_CHANNELPOOLING

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
                lock(syncModelCreate)
                {
                    model = new AmqpModelContainer(conn.CreateModel(), flags, this);
                }
            }

            return model;


#else
            lock(syncModelCreate)
            {
                return new AmqpModelContainer( conn.CreateModel(), flags, this);
            }
#endif

        }

        internal void ReturnModel(AmqpModelContainer modelContainer)
        {
            // NOTE: do not call AmqpModelContainer.Close() here.
            // That method calls this method.

#if !DISABLE_CHANNELPOOLING

            if (_disposed || HasModelExpired(Environment.TickCount, modelContainer))
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
    }
}
