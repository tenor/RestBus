using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using System;
using System.IO;

namespace RestBus.RabbitMQ
{
    internal static class Utils
    {
        const string exchangePrefix = "restbus:";
        const string workQueuePrefix = "restbus-wq-";
        const string callbackQueuePrefix = "restbus-cq-";
        const string subscriberQueuePrefix = "restbus-sq-";
        const string workQueueRoutingKey = "";

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

        public static string GetWorkQueueRoutingKey()
        {
            return workQueueRoutingKey;
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

        public static void DeclareExchangeAndQueues(global::RabbitMQ.Client.IModel channel, ExchangeInfo exchangeInfo, object syncObject, string subscriberId )
        {
            //TODO: IS the lock statement here necessary?
            lock (syncObject)
            {
                string exchangeName = Utils.GetExchangeName(exchangeInfo);
                string workQueueName = Utils.GetWorkQueueName(exchangeInfo);

                if (exchangeInfo.Exchange != "")
                {
                    //TODO: If Queues are durable then exchange ought to be too.
                    channel.ExchangeDeclare(exchangeName, exchangeInfo.ExchangeType, false, true, null);
                }

                var workQueueArgs = new System.Collections.Hashtable();
                workQueueArgs.Add("x-expires", (long)Utils.GetWorkQueueExpiry().TotalMilliseconds);

                //TODO: the line below can throw some kind of socket exception, so what do you do in that situation
                //Bear in mind that Restart may call this code.
                //The exception name is the OperationInterruptedException

                //Declare work queue
                channel.QueueDeclare(workQueueName, false, false, false, workQueueArgs);
                channel.QueueBind(workQueueName, exchangeName, Utils.GetWorkQueueRoutingKey());

				if(subscriberId != null)
				{
                    string subscriberQueueName = Utils.GetSubscriberQueueName(exchangeInfo, subscriberId);
	
	                var subscriberQueueArgs = new System.Collections.Hashtable();
	                subscriberQueueArgs.Add("x-expires", (long)Utils.GetSubscriberQueueExpiry().TotalMilliseconds);
	
	                //TODO: the line below can throw some kind of socket exception, so what do you do in that situation
	                //Bear in mind that Restart may call this code.
	                //The exception name is the OperationInterruptedException
	
	                //Declare subscriber queue
	                channel.QueueDeclare(subscriberQueueName, false, false, true, subscriberQueueArgs);
	                channel.QueueBind(subscriberQueueName, exchangeName, subscriberId);
				}

            }
        }


        public static byte[] SerializeAsBson(object obj)
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

        public static T DeserializeFromBson<T>(byte[] data)
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
