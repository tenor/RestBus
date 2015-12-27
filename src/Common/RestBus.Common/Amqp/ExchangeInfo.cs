using System;

namespace RestBus.Common.Amqp
{
    //TODO: Describe what this class does.
    public class ExchangeInfo
    {
        public ExchangeInfo(string serverAddress, string serviceName)
        {
            //TODO: Check for invalid parameters

            //TODO: Have a static IsValidExchangeOrQueueName that this method checks, the BasicMessageMapper will check that too for servicenames.

            this.ServerAddress = serverAddress;
            this.ServiceName = (serviceName ?? String.Empty).Trim();
            this.SupportedKinds = ExchangeKind.Direct;
        }

        public string ServerAddress { get; protected set; }
        public string ServiceName { get; protected set; }
        public ExchangeKind SupportedKinds { get; protected set; }

        /// <summary>
        /// Controls the message delivery mode.
        /// Set to true to persist messages to disk and false to not.
        /// This property has no effect if the work queue is non durable.
        /// <seealso cref="PersistentWorkQueuesAndExchanges"/>
        /// </summary>
        public bool PersistentMessages
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Controls the durability of work queues and exchanges.
        /// Set to true to make work queues and exchanges durable. i.e. survive a server restart, and to false to make work queues transient. 
        /// </summary>
        /// <remarks>
        /// This property only controls the durabilty of work queues and exchanges. It doesn't control the durability of messages sent to the work queue.
        /// <seealso cref="PersistentMessages"/>
        /// </remarks>
        public bool PersistentWorkQueuesAndExchanges
        {
            get
            {
                return false;
            }
        }
    }
}
