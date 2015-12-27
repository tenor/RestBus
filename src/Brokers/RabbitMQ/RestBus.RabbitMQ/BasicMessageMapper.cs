using RestBus.Client;
using RestBus.Common.Amqp;
using System;
using System.Net.Http;
using System.Collections.Generic;

namespace RestBus.RabbitMQ
{
    public class BasicMessageMapper : IMessageMapper
    {
        protected string amqpHostUri;
        protected string serviceName;

        public BasicMessageMapper(string amqpHostUri, string serviceName)
        {
            if(String.IsNullOrWhiteSpace(amqpHostUri))
            {
                throw new ArgumentException("amqpHostUri");
            }

            if (String.IsNullOrWhiteSpace(serviceName))
            {
                throw new ArgumentException("serviceName");
            }

            this.amqpHostUri = amqpHostUri;
            this.serviceName = serviceName;
        }

        public virtual ExchangeInfo GetExchangeInfo()
        {
            return new ExchangeInfo(amqpHostUri, serviceName, ExchangeKind.Topic);
        }

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

        public virtual string GetRoutingKey(HttpRequestMessage request)
        {
            return null;
        }

        /// <summary>
        /// Gets the Headers for the message.
        /// </summary>
        /// <remarks>
        /// This is only useful for the headers exchange type.
        /// </remarks>
        /// <param name="request"></param>
        /// <returns></returns>
        public IDictionary<string, object> GetHeaders(HttpRequestMessage request)
        {
            return null;
        }

        public virtual bool GetExpires(HttpRequestMessage request)
        {
            return true;
        }

        //TODO: Remove this, it isn't used anywhere
        protected static string GetPath(Uri uri)
        {
            if (uri == null) return string.Empty;

            string path = uri.PathAndQuery ?? string.Empty;

            if (path.Contains("?"))
            {
                path = path.Substring(0, path.Length - path.IndexOf('?'));
            }

            return path.Trim();
        }

        /// <summary>
        /// Returns the RequestOptions associated with a specified request.
        /// </summary>
        /// <remarks>
        /// This helper is useful for classes deriving from BasicMessageMapper.
        /// </remarks>
        /// <param name="request"></param>
        /// <returns></returns>
        protected RequestOptions GetRequestOptions(HttpRequestMessage request)
        {
            return MessageInvokerBase.GetRequestOptions(request);
        }
    }

}
