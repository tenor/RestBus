using RestBus.Client;
using RestBus.Common.Amqp;
using System;
using System.Linq;
using System.Net.Http;
using System.Collections.Generic;

namespace RestBus.RabbitMQ
{
    public class BasicMessageMapper : IMessageMapper
    {
        protected readonly string[] amqpHostUris;
        protected readonly string serviceName;

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

            this.amqpHostUris = new string[] { amqpHostUri };
            this.serviceName = serviceName;
        }

        public virtual ExchangeConfiguration GetExchangeConfig()
        {
            var connectionInfos = amqpHostUris.Select(u => new AmqpConnectionInfo { Uri = u, FriendlyName = StripUserInfoAndQuery(u) }).ToArray();
            return new ExchangeConfiguration(connectionInfos);
        }

        public virtual string GetServiceName(HttpRequestMessage request)
        {
            return serviceName;
        }

        public virtual string GetRoutingKey(HttpRequestMessage request, ExchangeKind exchangeKind)
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
        public virtual IDictionary<string, object> GetHeaders(HttpRequestMessage request)
        {
            return null;
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

        /// <summary>
        ///  Removes the username, password and query components of an AMQP uri.
        /// </summary>
        protected string StripUserInfoAndQuery(string amqpUri)
        {
            if(amqpUri == null)
            {
                throw new ArgumentNullException("amqpUri");
            }

            amqpUri = amqpUri.Trim();

            int startIndex;
            if(amqpUri.Length > 8 && amqpUri.StartsWith("amqps://", StringComparison.InvariantCultureIgnoreCase))
            {
                startIndex = 8;
            }
            else if (amqpUri.Length > 7 && amqpUri.StartsWith("amqp://", StringComparison.InvariantCultureIgnoreCase))
            {
                startIndex = 7;
            }
            else
            {
                throw new ArgumentException("amqpUri is not in expected format.");
            }

            int endIndex = amqpUri.IndexOf('@');
            if (endIndex >= 0)
            {
                amqpUri = amqpUri.Remove(startIndex, (endIndex - startIndex) + 1);
            }

            int queryIndex = amqpUri.IndexOf('?');
            if (queryIndex >= 0)
            {
                amqpUri = amqpUri.Substring(0, queryIndex);
            }

            return amqpUri;
        }
    }

}
