using RestBus.Client;
using RestBus.Common.Amqp;
using System;
using System.Net.Http;

namespace RestBus.RabbitMQ
{
    public class BasicMessageMapper : IMessageMapper
    {
        string serviceName;
        string amqpHostUri;

        public BasicMessageMapper(string amqpHostUri, string serviceName)
        {
            if(String.IsNullOrWhiteSpace(amqpHostUri))
            {
                throw new ArgumentException("rabbitMQHost");
            }

            if (String.IsNullOrWhiteSpace(serviceName))
            {
                throw new ArgumentException("applicationBaseUrl");
            }

            this.serviceName = serviceName;
            this.amqpHostUri = amqpHostUri;
        }

        public virtual ExchangeInfo GetExchangeInfo()
        {
            string appPath = serviceName;
            if (String.IsNullOrWhiteSpace(appPath))
            {
                appPath = "/";
            }

            appPath = appPath.Trim();
            if (!appPath.StartsWith("/"))
            {
                appPath = "/" + appPath;
            }
            if (!appPath.EndsWith("/"))
            {
                appPath = appPath + "/";
            }

            return new ExchangeInfo(amqpHostUri, appPath, "direct");
        }

        public virtual string GetRoutingKey(HttpRequestMessage request)
        {
            return null;
        }


        public bool GetExpires(HttpRequestMessage request)
        {
            return true;
        }

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

        protected RequestOptions GetRequestOptions(HttpRequestMessage request)
        {
            return MessageInvokerBase.GetRequestOptions(request);
        }

    }

}
