using RabbitMQ.Client;
using RabbitMQ.Client.Framing.v0_9_1;
using RestBus.RabbitMQ.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RestBus.RabbitMQ.Subscriber
{
    public class RestBusSubscriber : IRestBusSubscriber
    {
        //TODO: Error handling on the subscriber when the queue(s) expires
        
        IConnection conn;
        IModel workChannel;
        IModel subscriberChannel;
        string exchangeName;
        QueueingBasicConsumer workConsumer;
        QueueingBasicConsumer subscriberConsumer;

        readonly string workQueueName;
        readonly string subscriberQueueName;
        readonly TimeSpan workQueueExpiry;
        readonly TimeSpan subscriberQueueExpiry;

        object exchangeDeclareSync = new object();

        string subscriberId;
        ExchangeInfo exchangeInfo;
        bool isStarted = false;

        global::RabbitMQ.Util.SharedQueue lastProcessedQueue = null;

        readonly ConnectionFactory connectionFactory;

        public RestBusSubscriber(IExchangeMapper exchangeMapper )
        {

            exchangeInfo = exchangeMapper.GetExchangeInfo();
            subscriberId = Utils.GetRandomId();
            exchangeName = Utils.GetExchangeName(exchangeInfo);
            workQueueName = Utils.GetWorkQueueName(exchangeInfo);
            subscriberQueueName = Utils.GetSubscriberQueueName(exchangeInfo, subscriberId);
            workQueueExpiry = Utils.GetWorkQueueExpiry();
            subscriberQueueExpiry = Utils.GetSubscriberQueueExpiry();

            this.connectionFactory = new ConnectionFactory();
            connectionFactory.Uri = exchangeInfo.ServerAddress;
        }

        public void Start()
        {
            if (isStarted) return;
            isStarted = true;

            Restart();


        }

        public void Restart()
        {
            isStarted = true;

            //CLose connections and channels
            if (subscriberChannel != null)
            {
                try
                {
                    subscriberChannel.Close();
                }
                catch 
                {
                }
            }

            if (workChannel != null)
            {
                try
                {
                    workChannel.Close();
                }
                catch
                {
                }
            }

            if (conn != null)
            {
                try
                {
                    conn.Close();
                }
                catch
                {
                }
            }


            //TODO: CreateConnection() can always throw BrokerUnreachableException so keep that in mind when calling
            conn = connectionFactory.CreateConnection();

            //Create shared queue
            global::RabbitMQ.Util.SharedQueue queue = new global::RabbitMQ.Util.SharedQueue();

            //Create work channel and declare exchanges and queues
            workChannel = conn.CreateModel();
            DeclareExchangeAndQueues(workChannel);

            //Listen on work queue
            workConsumer = new QueueingBasicConsumer(workChannel, queue);
            string workQueueName = Utils.GetWorkQueueName(exchangeInfo);
            workChannel.BasicConsume(workQueueName, false, workConsumer);

            //Listen on subscriber queue
            subscriberChannel = conn.CreateModel();
            subscriberConsumer = new QueueingBasicConsumer(subscriberChannel, queue);
            string subscriberWorkQueueName = Utils.GetSubscriberQueueName(exchangeInfo, subscriberId);
            subscriberChannel.BasicConsume(subscriberWorkQueueName, false, subscriberConsumer);
        }

        //Will block until a request is received from either queue
        public HttpContext Dequeue()
        {
            if(workConsumer == null || subscriberConsumer == null) throw new InvalidOperationException("Start the subscriber prior to calling Dequeue");

            HttpRequestPacket request;
            IBasicProperties properties;

            global::RabbitMQ.Util.SharedQueue queue1 = null, queue2 = null;


            while (true)
            {
                if (lastProcessedQueue == subscriberConsumer.Queue)
                {
                    queue1 = workConsumer.Queue;
                    queue2 = subscriberConsumer.Queue;
                }
                else
                {
                    queue1 = subscriberConsumer.Queue;
                    queue2 = workConsumer.Queue;
                }

                if (TryGetRequest(queue1, out request, out properties))
                {
                    lastProcessedQueue = queue1;
                    break;
                }

                if (TryGetRequest(queue2, out request, out properties))
                {
                    lastProcessedQueue = queue2;
                    break;
                }

                 Thread.Sleep(1);

            }

            return new HttpContext { Request = request, ReplyToQueue = properties == null ? null : properties.ReplyTo, CorrelationId = properties.CorrelationId };


        }

        private bool TryGetRequest(global::RabbitMQ.Util.SharedQueue queue, out HttpRequestPacket request, out IBasicProperties properties)
        {
            object obj;
            global::RabbitMQ.Client.Events.BasicDeliverEventArgs evt;
            request = null;
            properties = null;

            obj = queue.DequeueNoWait(null);
            if (obj != null)
            {
                 evt = (global::RabbitMQ.Client.Events.BasicDeliverEventArgs)obj;
            }
            else
            {
                return false;
            }


            //Get message properties
            properties = evt.BasicProperties;

            //Deserialize message
            bool wasDeserialized = true;

            try
            {
                request = Utils.Deserialize<HttpRequestPacket>(evt.Body);
            }
            catch
            {

                wasDeserialized = false;
            }


            //Ack or reject message
            if (evt.ConsumerTag == workConsumer.ConsumerTag)
            {
                if (wasDeserialized)
                {
                    workConsumer.Model.BasicAck(evt.DeliveryTag, false);
                    return true;
                }
                else
                {
                    workConsumer.Model.BasicReject(evt.DeliveryTag, false);
                    return false;
                }
            }
            else if (evt.ConsumerTag == subscriberConsumer.ConsumerTag)
            {
                if (wasDeserialized)
                {
                    subscriberConsumer.Model.BasicAck(evt.DeliveryTag, false);
                    return true;
                }
                else
                {
                    subscriberConsumer.Model.BasicReject(evt.DeliveryTag, false);
                    return false;
                }
            }
            else
            {
                throw new InvalidOperationException("Message was dequeued by an unexpected/unknown consumer");
            }

        }

        public void Dispose()
        {
            if (workChannel != null)
            {
                workChannel.Dispose();
            }

            if (subscriberChannel != null)
            {
                subscriberChannel.Dispose();
            }

            if (conn != null)
            {
                conn.Dispose();
            }
        }

        private void DeclareExchangeAndQueues(IModel channel)
        {
            lock (exchangeDeclareSync)
            {
                if (exchangeInfo.Exchange != "")
                {
                    channel.ExchangeDeclare(exchangeName, exchangeInfo.ExchangeType);
                }

                var workQueueArgs = new System.Collections.Hashtable();
                workQueueArgs.Add("x-expires", (long)workQueueExpiry.TotalMilliseconds);

                var subscriberQueueArgs = new System.Collections.Hashtable();
                subscriberQueueArgs.Add("x-expires", (long)subscriberQueueExpiry.TotalMilliseconds);

                //TODO: the line below can throw some kind of socket exception, so what do you do in that situation
                //Bear in mind that Restart may call this code.
                //The exception name is the OperationInterruptedException

                //Declare work queue
                channel.QueueDeclare(workQueueName, false, false, false, workQueueArgs);
                channel.QueueBind(workQueueName, exchangeName, exchangeName);

                //Declare subscriber queue
                channel.QueueDeclare(subscriberQueueName, false, false, true, subscriberQueueArgs);

            }
        }

        public void SendResponse(HttpContext context, HttpResponsePacket response )
        {
            if (String.IsNullOrEmpty(context.ReplyToQueue)) return;

            if (conn == null)
            {
                //TODO: Log this -- it technically shouldn't happen. Also translate to a HTTP Unreachable because it means StartCallbackQueueConsumer didn't create a connection
                throw new ApplicationException("This is Bad");
            }

            //P.S: Do not share channels across threads.
            using (IModel channel = conn.CreateModel())
            {

                BasicProperties basicProperties = new BasicProperties { CorrelationId = context.CorrelationId };

                try
                {
                    channel.BasicPublish(String.Empty,
                                    context.ReplyToQueue,
                                    basicProperties,
                                    Utils.Serialize(response));
                }
                catch { }
            }

        }

    
    }
}
