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
        protected string[] amqpHostUris;
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

            this.amqpHostUris = new string[] { amqpHostUri };
            this.serviceName = serviceName;
        }

        public virtual ExchangeConfiguration GetExchangeConfig()
        {
            var connectionInfos = amqpHostUris.Select(u => new AmqpConnectionInfo { Uri = u, FriendlyName = StripUserInfo(u) }).ToList();
            return new ExchangeConfiguration(connectionInfos, serviceName);
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
        public virtual IDictionary<string, object> GetHeaders(HttpRequestMessage request)
        {
            return null;
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

        /// <summary>
        ///  Removes the username and password components of an AMQP uri
        /// </summary>
        protected string StripUserInfo(string amqpUri)
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
            if(endIndex == -1) throw new ArgumentException("amqpUri is not in expected format.");

            return amqpUri.Remove(startIndex, (endIndex - startIndex) + 1);
        }
    }

}
