using RabbitMQ.Client;
using RabbitMQ.Client.Framing.v0_9_1;
using RestBus.RabbitMQ;
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
        QueueingBasicConsumer workConsumer;
        QueueingBasicConsumer subscriberConsumer;
        string subscriberId;
        ExchangeInfo exchangeInfo;
        object exchangeDeclareSync = new object();
        volatile bool disposed = false;

        //TODO: Consider converting this to an int so that you can do Interlocked.Exchange here(Is that neccessary?)
        bool isStarted = false;
        global::RabbitMQ.Util.SharedQueue lastProcessedQueue = null;
        readonly ConnectionFactory connectionFactory;
        public const string SUBSCRIBER_ID_HEADER = "X-RestBus-Subscriber-Id";

        public RestBusSubscriber(IMessageMapper messageMapper )
        {

            exchangeInfo = messageMapper.GetExchangeInfo();
            subscriberId = Utils.GetRandomId();

            this.connectionFactory = new ConnectionFactory();
            connectionFactory.Uri = exchangeInfo.ServerAddress;
            connectionFactory.RequestedHeartbeat = Client.RestBusClient.HEART_BEAT;

        }

        public string Id
        {
            get { return subscriberId; }
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

                try
                {
                    conn.Dispose();
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
            
            /* Work this into subscriber dispose and restart
            //Cancel consumers on server
            if(workCTag != null)
            {
                try
                {
                    workChannel.BasicCancel(workCTag);
                }
                catch { }
            }

            if (subscriberCTag != null)
            {
                try
                {
                    workChannel.BasicCancel(subscriberCTag);
                }
                catch { }
            }
             */

            //Recdeclare exchanges and queues
            Utils.DeclareExchangeAndQueues(workChannel, exchangeInfo, exchangeDeclareSync, subscriberId);

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
            if (disposed) throw new ObjectDisposedException("Subscriber has been disposed");
            if(workConsumer == null || subscriberConsumer == null) throw new InvalidOperationException("Start the subscriber prior to calling Dequeue");

            HttpRequestPacket request;
            IBasicProperties properties;

            global::RabbitMQ.Util.SharedQueue queue1 = null, queue2 = null;

            while (true)
            {
                if (disposed) throw new ObjectDisposedException("Subscriber has been disposed");
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

                try
                {
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
                }
                catch (Exception e)
                {
                    if (!(e is System.IO.EndOfStreamException))
                    {
                        //TODO: Log exception: Don't know what else to expect here

                    }

                    //Loop until a connection is made
                    bool successfulRestart = false;
                    while (true)
                    {
                        try
                        {
                            Restart();
                            successfulRestart = true;
                        }
                        catch { }

                        if (disposed) throw new ObjectDisposedException("Subscriber has been disposed");

                        if (successfulRestart) break;
                        Thread.Sleep(1);
                    }

                    //Check for next message
                    continue;
                }

                Thread.Sleep(1); //Nothing was found in both queues so take a 1ms nap

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
                request = HttpRequestPacket.Deserialize(evt.Body);

                //Add/Update Content-Length Header
                request.Headers["Content-Length"] = new string[] { (request.Content == null ? 0 : request.Content.Length).ToString() };

                //Add/Update Subscriber-Id header
                request.Headers[SUBSCRIBER_ID_HEADER] = new string[] { this.subscriberId };

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
            disposed = true;
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
                try
                {
                    conn.Close();
                }
                catch { }

                try
                {
                    conn.Dispose();
                }
                catch { }
            }
        }

        public void SendResponse(HttpContext context, HttpResponsePacket response )
        {
            if (disposed) throw new ObjectDisposedException("Subscriber has been disposed");
            if (String.IsNullOrEmpty(context.ReplyToQueue)) return;

            if (conn == null)
            {
                //TODO: Log this -- it technically shouldn't happen. Also translate to a HTTP Unreachable because it means StartCallbackQueueConsumer didn't create a connection
                throw new ApplicationException("This is Bad");
            }

            //Note: Do not share channels across threads.
            using (IModel channel = conn.CreateModel())
            {

                BasicProperties basicProperties = new BasicProperties { CorrelationId = context.CorrelationId };

                try
                {
                    channel.BasicPublish(String.Empty,
                                    context.ReplyToQueue,
                                    basicProperties,
                                    response.Serialize());
                }
                catch { }
            }

        }

    }
}
