using RabbitMQ.Client;
using RestBus.Common.Amqp;
using System;
using System.Collections.Generic;

namespace RestBus.RabbitMQ
{
    internal static class AmqpUtils
	{
		const string exchangePrefix = "restbus:/";
        const string queuePrefix = "restbus";
        const string workQueuePath = "/wq";
        const string callbackQueuePath = "/cq";
        const string subscriberQueuePath = "/sq";
		const string workQueueRoutingKey = "";

        public const int DEFAULT_PREFETCH_COUNT = 50; //Based on measurements from http://www.rabbitmq.com/blog/2012/04/25/rabbitmq-performance-measurements-part-2/

        public static string GetCallbackQueueName(string serviceName, string clientId)
		{
			return queuePrefix + PrefixSlashIfNotEmpty(serviceName) + callbackQueuePath + "/" + clientId;
		}

		public static string GetSubscriberQueueName(string serviceName, string subscriberId)
		{
			return queuePrefix + PrefixSlashIfNotEmpty(serviceName) + subscriberQueuePath + "/" + subscriberId;
		}

		public static string GetWorkQueueName(ExchangeConfiguration exchangeConfig, string serviceName)
		{
            return queuePrefix + PrefixSlashIfNotEmpty(serviceName) + workQueuePath + (exchangeConfig.PersistentWorkQueuesAndExchanges ? "/persistent" : String.Empty);
        }

		public static string GetExchangeName(ExchangeConfiguration exchangeConfig, string serviceName, ExchangeKind kind)
		{
			return exchangePrefix + serviceName + "." + GetExchangeKindName(kind) + (exchangeConfig.PersistentWorkQueuesAndExchanges ? ".persistent" : String.Empty);
		}

		public static string GetWorkQueueRoutingKey()
		{
			return workQueueRoutingKey;
		}

		public static TimeSpan GetWorkQueueExpiry()
		{
			return TimeSpan.FromHours(24);
		}

		public static TimeSpan GetCallbackQueueExpiry()
		{
			return TimeSpan.FromMinutes(4);
		}

		public static TimeSpan GetSubscriberQueueExpiry()
		{
			return TimeSpan.FromMinutes(4);
		}

        //This method is used to generate queue names for exclusive queues because there is a chance 
        //that by using the SequenceGenerator, two instances (processes or threads) of a client or server
        //started silmulataneously will generate the same same sequence since it's time based.
        public static string GetNewExclusiveQueueId()
        {
            byte[] buffer = new byte[8];
            Common.SynchronizedRandom.Instance.NextBytes(buffer);
            var base64str = Convert.ToBase64String(buffer, Base64FormattingOptions.None);

            //Return a url-safe variant of Base64 where + becomes ~, / becomes - and there is no padding (=)
            return base64str.Replace('+', '~').Replace('/', '-').TrimEnd('=');
        }


		//NOTE This is the only method that cannot be moved into RestBus.Common so keep that in mind if integrating other Amqp brokers
		public static void DeclareExchangeAndQueues(IModel channel, ExchangeConfiguration exchangeConfig, string serviceName, object syncObject, string subscriberId )
		{
			//TODO: IS the lock statement here necessary?
			lock (syncObject)
			{
                //TODO: Other Exchange types.

				string exchangeName = AmqpUtils.GetExchangeName(exchangeConfig, serviceName, ExchangeKind.Direct);
				string workQueueName = AmqpUtils.GetWorkQueueName(exchangeConfig, serviceName);

				if (serviceName != "")
				{
                    //Declare direct exchange
                    if (exchangeConfig.SupportedKinds.HasFlag(ExchangeKind.Direct))
                    {
                        channel.ExchangeDeclare(exchangeName, AmqpUtils.GetExchangeKindName(ExchangeKind.Direct), exchangeConfig.PersistentWorkQueuesAndExchanges, !exchangeConfig.PersistentWorkQueuesAndExchanges, null);
                    }
				}

                Dictionary<string, object> workQueueArgs = null;
                if (!exchangeConfig.PersistentWorkQueuesAndExchanges)
                {
                    workQueueArgs = new Dictionary<string, object>();
                    workQueueArgs.Add("x-expires", (long)AmqpUtils.GetWorkQueueExpiry().TotalMilliseconds);
                }

				//TODO: the line below can throw some kind of socket exception, so what do you do in that situation
				//Bear in mind that Restart may call this code.
				//The exception name is the OperationInterruptedException

				//Declare work queue
				channel.QueueDeclare(workQueueName, exchangeConfig.PersistentWorkQueuesAndExchanges, false, false, workQueueArgs);
				channel.QueueBind(workQueueName, exchangeName, AmqpUtils.GetWorkQueueRoutingKey());

				if(subscriberId != null)
				{
					string subscriberQueueName = AmqpUtils.GetSubscriberQueueName(serviceName, subscriberId);

                    //The queue is set to be auto deleted once the last consumer stops using it.
                    //However, RabbitMQ will not delete the queue if no consumer ever got to use it.
                    //Passing x-expires in solves that: It tells RabbitMQ to delete the queue, if no one uses it within the specified time.

                    var subscriberQueueArgs = new Dictionary<string, object>();
					subscriberQueueArgs.Add("x-expires", (long)AmqpUtils.GetSubscriberQueueExpiry().TotalMilliseconds);

					//TODO: Look into making the subscriber queue exclusive
					//and retry with a different id if the queue has alreasy been taken.
					//in this case the Id property of the ISubscriber interface should be changed to GetId()
					//and will be documented to return the "current" id.
					//In fact Hide GetId and id property for both client and subscriber, they should be private for now.
					//Write something similar for the client.

					//TODO: the line below can throw some kind of socket exception, so what do you do in that situation
					//Bear in mind that Restart may call this code.
					//The exception name is the OperationInterruptedException
	
					//Declare subscriber queue
					channel.QueueDeclare(subscriberQueueName, false, false, true, subscriberQueueArgs);
					channel.QueueBind(subscriberQueueName, exchangeName, subscriberId);
				}

			}
		}

        public static string GetExchangeKindName(ExchangeKind kind)
        {
            switch (kind)
            {
                case ExchangeKind.Direct:
                    return "direct";
                case ExchangeKind.Fanout:
                    return "fanout";
                case ExchangeKind.Headers:
                    return "headers";
                case ExchangeKind.Topic:
                    return "topic";
                default:
                    return String.Empty;
            }
        }

        private static string PrefixSlashIfNotEmpty(string name)
        {
            if (String.IsNullOrEmpty(name)) return String.Empty;
            return "/" + name;
        }
	}
}
