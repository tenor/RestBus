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
            return new ExchangeInfo(amqpHostUri, serviceName);
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
        private static string GetPath(Uri uri)
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
