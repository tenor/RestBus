using System;

namespace RestBus.Common
{
    /// <summary>
    /// Provides a synchronized (thread safe) random value generator
    /// </summary>
    public class SynchronizedRandom
    {
        object sync = new object();
        Random _rnd = new Random();

        static Lazy<SynchronizedRandom> instance = new Lazy<SynchronizedRandom>( () => { return new SynchronizedRandom(); });

        private SynchronizedRandom()
        {
           
        }

        public static SynchronizedRandom Instance
        {
            get
            {
                return instance.Value;
            }
        }

        public int Next()
        {
            lock(sync)
            {
                return _rnd.Next();
            }
        }

        public int Next (int maxValue)
        {
            lock (sync)
            {
                return _rnd.Next(maxValue);
            }
        }

        public double NextDouble()
        {
            lock (sync)
            {
                return _rnd.NextDouble();
            }
        }

        public void NextBytes(byte[] buffer)
        {
            lock (sync)
            {
                _rnd.NextBytes(buffer);
            }
        }

    }
}
