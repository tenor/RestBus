using RabbitMQ.Client;
using RestBus.Common.Amqp;
using System;
using System.Collections.Generic;

namespace RestBus.RabbitMQ
{
    internal static class AmqpUtils
	{
		const string exchangePrefix = "restbus:";
        const string queuePrefix = "restbus";
        const string workQueuePath = "wq";
        const string callbackQueuePath = "cq/";
        const string subscriberQueuePath = "sq/";
		const string workQueueRoutingKey = "";

		public static string GetCallbackQueueName(ExchangeInfo exchangeInfo, string clientId)
		{
			return queuePrefix + exchangeInfo.Name + callbackQueuePath + clientId;
		}

		public static string GetSubscriberQueueName(ExchangeInfo exchangeInfo, string subscriberId)
		{
			return queuePrefix + exchangeInfo.Name + subscriberQueuePath + subscriberId;
		}

		public static string GetWorkQueueName(ExchangeInfo exchangeInfo)
		{
			return queuePrefix + exchangeInfo.Name + workQueuePath;
		}

		public static string GetExchangeName(ExchangeInfo exchangeInfo)
		{
			return exchangePrefix + exchangeInfo.Name;
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
			return TimeSpan.FromMinutes(1);
		}

		public static TimeSpan GetSubscriberQueueExpiry()
		{
			return TimeSpan.FromMinutes(1);
		}

        //This method is used to generate queue names for exclusive queues because there is a chance 
        //that by using the SequenceGenerator, two instances (processes or threads) of a client or server
        //started silmulataneously will generate the same same sequence since it's time based.
        public static string GetNewExclusiveQueueId()
        {
            byte[] buffer = new byte[8];
            Common.SynchronizedRandom.Instance.NextBytes(buffer);
            var base64str = Convert.ToBase64String(buffer, Base64FormattingOptions.None);

            //Return a url-safe variant of Base64 where + becomes ~ and / become -
            return base64str.Replace('+', '~').Replace('/', '-');
        }


		//NOTE This is the only method that cannot be moved into RestBus.Common so keep that in mind if intergrating other Amqp brokers
		public static void DeclareExchangeAndQueues(IModel channel, ExchangeInfo exchangeInfo, object syncObject, string subscriberId )
		{
			//TODO: IS the lock statement here necessary?
			lock (syncObject)
			{
				string exchangeName = AmqpUtils.GetExchangeName(exchangeInfo);
				string workQueueName = AmqpUtils.GetWorkQueueName(exchangeInfo);

				if (exchangeInfo.Name != "")
				{
                    //TODO: If Queues are durable then exchange ought to be too.

                    //Declare direct exchange
                    if (exchangeInfo.SupportedKinds.HasFlag(ExchangeKind.Direct))
                    {
                        channel.ExchangeDeclare(exchangeName, "direct", false, true, null);
                    }
				}

                //The queue is set to be auto deleted once the last consumer stops using it.
                //However, RabbitMQ will not delete the queue if no consumer ever got to use it.
                //Passing x-expires in solves that: It tells RabbitMQ to delete the queue, if no one uses it within the specified time.

                var workQueueArgs = new Dictionary<string, object>();
				workQueueArgs.Add("x-expires", (long)AmqpUtils.GetWorkQueueExpiry().TotalMilliseconds);

				//TODO: the line below can throw some kind of socket exception, so what do you do in that situation
				//Bear in mind that Restart may call this code.
				//The exception name is the OperationInterruptedException

				//Declare work queue
				channel.QueueDeclare(workQueueName, false, false, false, workQueueArgs);
				channel.QueueBind(workQueueName, exchangeName, AmqpUtils.GetWorkQueueRoutingKey());

				if(subscriberId != null)
				{
					string subscriberQueueName = AmqpUtils.GetSubscriberQueueName(exchangeInfo, subscriberId);

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


	}
}
