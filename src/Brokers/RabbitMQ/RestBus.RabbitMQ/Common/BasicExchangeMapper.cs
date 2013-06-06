using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace RestBus.RabbitMQ
{
    //TODO: Is this class used by the subscriber at all. If not move to Client, same goes for the Interface
    public class BasicExchangeMapper : RestBus.RabbitMQ.IExchangeMapper
    {
        string applicationName;
        string rabbitMQHost;


        public BasicExchangeMapper(string rabbitMQHost, string applicationName)
        {
            if(String.IsNullOrWhiteSpace(rabbitMQHost))
            {
                throw new ArgumentException("rabbitMQHost");
            }

            if (String.IsNullOrWhiteSpace(applicationName))
            {
                throw new ArgumentException("applicationBaseUrl");
            }

            this.applicationName = applicationName;
            this.rabbitMQHost = rabbitMQHost;
        }

        public virtual ExchangeInfo GetExchangeInfo()
        {
            string appPath = applicationName;
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

            return new ExchangeInfo(rabbitMQHost, appPath, "direct");
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

    }

}
