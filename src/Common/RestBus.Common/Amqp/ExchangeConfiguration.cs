using System;
using System.Collections.Generic;
using System.Net.Http;

namespace RestBus.Common.Amqp
{
    //TODO: Describe what this class does.
    public class ExchangeConfiguration
    {
        public ExchangeConfiguration(IList<string> serverUris, string serviceName)
        {
            //TODO: Check for invalid parameters

            //TODO: Have a static IsValidExchangeOrQueueName that this method checks, the BasicMessageMapper will check that too for servicenames.

            //Check serverUris
            if (serverUris == null) throw new ArgumentNullException("serverUris");
            if (serverUris.Count == 0) throw new ArgumentException("serverUris must not be empty.");
            for(int i = 0; i < serverUris.Count; i++)
            {
                if (serverUris[i] == null) throw new ArgumentException("Index " + i + " of serverUris argument is null.");
            }

            this.ServerUris = serverUris;
            this.ServiceName = (serviceName ?? String.Empty).Trim();
            this.SupportedKinds = ExchangeKind.Direct;
        }

        public IList<string> ServerUris { get; protected set; }
        public string ServiceName { get; protected set; }
        public ExchangeKind SupportedKinds { get; set; }

        /// <summary>
        /// Controls the message delivery mode.
        /// Set to true to persist messages to disk and false to not.
        /// This property has no effect if the work queue is non durable.
        /// <seealso cref="PersistentWorkQueuesAndExchanges"/>
        /// </summary>
        public bool PersistentMessages { get; set; }

        /// <summary>
        /// Controls the durability of work queues and exchanges.
        /// Set to true to make work queues and exchanges durable. i.e. survive a server restart, and to false to make work queues transient. 
        /// </summary>
        /// <remarks>
        /// This property only controls the durabilty of work queues and exchanges. It doesn't control the durability of messages sent to the work queue.
        /// <seealso cref="PersistentMessages"/>
        /// </remarks>
        public bool PersistentWorkQueuesAndExchanges { get; set; }

        /// <summary>
        /// A function that returns an indicator that the message should expire.
        /// If not set, messages expire by default.
        /// </summary>
        public Func<HttpRequestMessage, bool> MessageExpires { get; set; }
    }
}
