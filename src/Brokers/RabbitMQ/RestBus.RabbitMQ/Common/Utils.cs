using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using System;
using System.IO;

namespace RestBus.RabbitMQ.Common
{
    internal static class Utils
    {
        const string exchangePrefix = "restbus:";
        const string workQueuePrefix = "restbus-wq-";
        const string callbackQueuePrefix = "restbus-cq-";
        const string subscriberQueuePrefix = "restbus-sq-";

        public static string GetCallbackQueueName(ExchangeInfo exchangeInfo, string clientId)
        {
            return callbackQueuePrefix + exchangeInfo.Exchange + clientId;
        }

        public static string GetSubscriberQueueName(ExchangeInfo exchangeInfo, string subscriberId)
        {
            return subscriberQueuePrefix + exchangeInfo.Exchange + subscriberId;
        }

        public static string GetWorkQueueName(ExchangeInfo exchangeInfo)
        {
            return workQueuePrefix + exchangeInfo.Exchange;
        }

        public static string GetExchangeName(ExchangeInfo exchangeInfo)
        {
            return exchangePrefix + exchangeInfo.Exchange;
        }

        public static string GetRandomId()
        {
            return ((long)(new Random().NextDouble() * (Int32.MaxValue - 1))).ToString("x");
        }

        public static TimeSpan GetWorkQueueExpiry()
        {
            return TimeSpan.FromHours(24);
        }


        public static TimeSpan GetCallbackQueueExpiry()
        {
            return TimeSpan.FromMinutes(1);
        }



        public static TimeSpan GetSubscriberQueueExpiry()
        {
            return TimeSpan.FromMinutes(1);
        }

        public static byte[] Serialize(object obj)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                JsonSerializer serializer = new JsonSerializer();

                using (BsonWriter writer = new BsonWriter(ms))
                {
                    serializer.Serialize(writer, obj);
                    return ms.ToArray();
                }

            }
        }

        public static T Deserialize<T>(byte[] data)
        {
            using (MemoryStream ms = new MemoryStream(data))
            {
                JsonSerializer serializer = new JsonSerializer();

                using (BsonReader reader = new BsonReader(ms))
                {
                    return serializer.Deserialize<T>(reader);
                }

            }
        }
    }
}
