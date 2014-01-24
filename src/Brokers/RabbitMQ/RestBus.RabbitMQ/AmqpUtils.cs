using RabbitMQ.Client;
using RestBus.Common.Amqp;
using RestBus.RabbitMQ.ChannelPooling;
using System;
using System.Collections.Generic;
using System.IO;

namespace RestBus.RabbitMQ
{
	internal static class AmqpUtils
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
            //NOTE: Might be better to have a static Random object instead of instatiating a new one everytime
            // However, Random.NextDouble() might not be thread safe.
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


		//NOTE This is the only method that cannot be moved into RestBus.Common so keep that in mind if intergrating other Amqp brokers
		public static void DeclareExchangeAndQueues(IModel channel, ExchangeInfo exchangeInfo, object syncObject, string subscriberId )
		{
			//TODO: IS the lock statement here necessary?
			lock (syncObject)
			{
				string exchangeName = AmqpUtils.GetExchangeName(exchangeInfo);
				string workQueueName = AmqpUtils.GetWorkQueueName(exchangeInfo);

				if (exchangeInfo.Exchange != "")
				{
					//TODO: If Queues are durable then exchange ought to be too.
					channel.ExchangeDeclare(exchangeName, exchangeInfo.ExchangeType, false, true, null);
				}

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
