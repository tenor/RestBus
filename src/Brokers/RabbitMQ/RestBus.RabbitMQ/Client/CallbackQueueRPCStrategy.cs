using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Framing;
using RestBus.Common.Amqp;
using RestBus.RabbitMQ.ChannelPooling;
using RestBus.RabbitMQ.Consumer;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace RestBus.RabbitMQ.Client
{
    internal class CallbackQueueRPCStrategy : IRPCStrategy
    {
        volatile ConcurrentQueueingConsumer callbackConsumer;
        volatile bool isInConsumerLoop;
        volatile bool consumerCancelled;
        volatile string callbackQueueName;
        volatile bool seenRequestExpectingResponse;
        volatile bool disposed = false;

        readonly string indirectReplyToQueueName;
        readonly ConcurrentDictionary<string, ExpectedResponse> expectedResponses;
        readonly ClientSettings clientSettings;
        readonly ManualResetEventSlim responseQueued = new ManualResetEventSlim();
        readonly CancellationTokenSource disposedCancellationSource = new CancellationTokenSource();
        readonly object restartConsumerSync = new object();

        public bool ReturnModelAfterSending
        {
            get
            {
                return true;
            }
        }

        public CallbackQueueRPCStrategy(ClientSettings clientSettings, ExchangeConfiguration exchangeConfig, ConnectionManager connectionManager)
        {
            this.clientSettings = clientSettings;
            this.indirectReplyToQueueName = AmqpUtils.GetCallbackQueueName(exchangeConfig, AmqpUtils.GetNewExclusiveQueueId());

            //Initialize expectedResponses
            expectedResponses = new ConcurrentDictionary<string, ExpectedResponse>();

        }

        public void StartStrategy(AmqpChannelPooler pool, bool requestExpectsResponse)
        {
            if (requestExpectsResponse) seenRequestExpectingResponse = true;

            if (seenRequestExpectingResponse)
            {
                //This client has seen a request expecting a response so
                //Start Callback consumer if it hasn't started
                StartCallbackQueueConsumer(pool);
            }
        }

        public ExpectedResponse PrepareForResponse(string correlationId, BasicProperties basicProperties, AmqpModelContainer model, HttpRequestMessage request, TimeSpan requestTimeout, CancellationToken cancellationToken, TaskCompletionSource<HttpResponseMessage>  taskSource)
        {
            //Set Reply to queue
            basicProperties.ReplyTo = callbackQueueName;

            //Initialize response arrival object and add to expected responses dictionary
            var arrival = new ExpectedResponse();
            expectedResponses[correlationId] = arrival;

            RPCStrategyHelpers.WaitForResponse(request, arrival, requestTimeout, model, false, cancellationToken, taskSource, () => CleanupMessagingResources(correlationId, arrival));

            return arrival;

        }

        public AmqpModelContainer GetModel(AmqpChannelPooler pool, bool streamsPublisherConfirms)
        {
            return pool.GetModel(streamsPublisherConfirms ? ChannelFlags.PublisherConfirms : ChannelFlags.None);
        }

        public void CleanupMessagingResources(string correlationId, ExpectedResponse expectedResponse)
        {
            if (!String.IsNullOrEmpty(correlationId))
            {
                ExpectedResponse unused;
                expectedResponses.TryRemove(correlationId, out unused);
            }

            if (expectedResponse != null)
            {
                expectedResponse.Dispose();
            }
        }


        private void StartCallbackQueueConsumer(AmqpChannelPooler pool)
        {
            //TODO: Double-checked locking -- make this better
            //TODO: Consider moving the conn related checks into a pooler method
            if (callbackConsumer == null || !isInConsumerLoop || !pool.Connection.IsOpen)
            {
                lock (restartConsumerSync)
                {
                    if (!(callbackConsumer == null || !isInConsumerLoop || !pool.Connection.IsOpen)) return;

                    //This method waits on this signal to make sure the callbackprocessor thread either started successfully or failed.
                    ManualResetEventSlim consumerSignal = new ManualResetEventSlim(false);
                    Exception consumerSignalException = null;

                    Thread callBackProcessor = new Thread(p =>
                    {
                        ConcurrentQueueingConsumer consumer = null;
                        try
                        {
                            //Start consumer
                            AmqpModelContainer channelContainer = null;
                            try
                            {
                                channelContainer = pool.GetModel(ChannelFlags.Consumer);
                                IModel channel = channelContainer.Channel;

                                if (clientSettings.DisableDirectReplies || !channelContainer.IsDirectReplyToCapable)
                                {
                                    DeclareIndirectReplyToQueue(channel, indirectReplyToQueueName);
                                }

                                consumer = new ConcurrentQueueingConsumer(channel, responseQueued);

                                //Set consumerCancelled to true on consumer cancellation
                                consumerCancelled = false;
                                consumer.ConsumerCancelled += (s, e) => { consumerCancelled = true; };

                                channel.BasicQos(0, 50, false);
                                //Start consumer:

                                string replyToQueueName;

                                if (clientSettings.DisableDirectReplies || !channelContainer.IsDirectReplyToCapable)
                                {
                                    channel.BasicConsume(indirectReplyToQueueName, clientSettings.AckBehavior == ClientAckBehavior.Automatic, consumer);
                                    replyToQueueName = indirectReplyToQueueName;
                                }
                                else
                                {
                                    channel.BasicConsume(RPCStrategyHelpers.DIRECT_REPLY_TO_QUEUENAME_ARG, true, consumer);

                                    //Discover direct reply to queue name
                                    replyToQueueName = DiscoverDirectReplyToQueueName(channel, indirectReplyToQueueName);
                                }

                                //Set callbackConsumer to consumer
                                callbackQueueName = replyToQueueName;
                                callbackConsumer = consumer;

                                //Notify outer thread that channel has started consumption
                                consumerSignal.Set();

                                BasicDeliverEventArgs evt;
                                ExpectedResponse expected;
                                isInConsumerLoop = true;

                                while (true)
                                {
                                    try
                                    {
                                        evt = DequeueCallbackQueue();
                                    }
                                    catch
                                    {
                                        //TODO: Log this exception except it's ObjectDisposedException or OperationCancelledException
                                        throw;
                                    }

                                    expected = null;
                                    if (!String.IsNullOrEmpty(evt.BasicProperties.CorrelationId) && expectedResponses.TryRemove(evt.BasicProperties.CorrelationId, out expected))
                                    {
                                        RPCStrategyHelpers.ReadAndSignalDelivery(expected, evt);
                                    }

                                    //Acknowledge receipt:
                                    //In ClientBehavior.Automatic mode
                                    //Client acks all received messages, even if it wasn't the expected one or even if it wasn't expecting anything.
                                    //This prevents a situation where crap messages are sent to the client but the good expected message is stuck behind the
                                    //crap ones and isn't delivered because the crap ones in front of the queue aren't acked and crap messages exceed prefetchCount.

                                    //In ClientAckBehavior.ValidResponses mode (and Direct Reply to is not in effect):
                                    //Client only acks expected messages if they could be deserialized
                                    //If not, they are rejected.

                                    if ((clientSettings.DisableDirectReplies || !channelContainer.IsDirectReplyToCapable) && clientSettings.AckBehavior == ClientAckBehavior.ValidResponses)
                                    {
                                        if (expected != null && expected.DeserializationException != null)
                                        {
                                            channel.BasicAck(evt.DeliveryTag, false);
                                        }
                                        else
                                        {
                                            channel.BasicReject(evt.DeliveryTag, false);
                                        }
                                    }

                                    //Exit loop if consumer is cancelled.
                                    if (consumerCancelled)
                                    {
                                        break;
                                    }
                                }

                            }
                            finally
                            {
                                isInConsumerLoop = false;
                                pool.SetRecycle();

                                if (channelContainer != null)
                                {
                                    if (consumer != null && !consumerCancelled)
                                    {
                                        try
                                        {
                                            channelContainer.Channel.BasicCancel(consumer.ConsumerTag);
                                        }
                                        catch { }
                                    }

                                    channelContainer.Close();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            //TODO: Log error (Except it's object disposed exception)

                            //Set Exception object which will be throw by signal waiter
                            consumerSignalException = ex;

                            //Notify outer thread to move on, in case it's still waiting
                            try
                            {
                                consumerSignal.Set();
                            }
                            catch { }
                        }
                        finally
                        {
                            if (pool != null)
                            {
                                pool.Dispose();
                            }
                        }

                    });

                    //Start Thread
                    callBackProcessor.Name = "RestBus RabbitMQ Client Callback Queue Consumer";
                    callBackProcessor.IsBackground = true;
                    callBackProcessor.Start();

                    //Wait for Thread to start consuming messages
                    consumerSignal.Wait();
                    consumerSignal.Dispose();

                    //Examine exception if it were set and rethrow it
                    Thread.MemoryBarrier(); //Ensure we have the non-cached version of consumerSignalException
                    if (consumerSignalException != null)
                    {
                        throw consumerSignalException;
                    }

                    //No more code from this point in this method

                }
            }

        }

        private BasicDeliverEventArgs DequeueCallbackQueue()
        {
            while (true)
            {
                if (disposed) throw new ObjectDisposedException(GetType().FullName);

                BasicDeliverEventArgs item;
                if (callbackConsumer.TryInstantDequeue(out item))
                {
                    return item;
                }

                //Throws OperationCanceledException if tken is cancelled
                responseQueued.Wait(disposedCancellationSource.Token);
                responseQueued.Reset();
            }
        }


        private static void DeclareIndirectReplyToQueue(IModel channel, string queueName)
        {
            //The queue is set to be auto deleted once the last consumer stops using it.
            //However, RabbitMQ will not delete the queue if no consumer ever got to use it.
            //Passing x-expires in solves that: It tells RabbitMQ to delete the queue, if no one uses it within the specified time.

            var callbackQueueArgs = new Dictionary<string, object>();
            callbackQueueArgs.Add("x-expires", (long)AmqpUtils.GetCallbackQueueExpiry().TotalMilliseconds);

            //TODO: AckBehavior is applied here.

            //Declare call back queue
            channel.QueueDeclare(queueName, false, false, true, callbackQueueArgs);
        }

        /// <summary>
        /// Discovers the Direct reply-to queue name ( https://www.rabbitmq.com/direct-reply-to.html ) by messaging itself.
        /// </summary>
        private static string DiscoverDirectReplyToQueueName(IModel channel, string indirectReplyToQueueName)
        {
            DeclareIndirectReplyToQueue(channel, indirectReplyToQueueName);

            var receiver = new ConcurrentQueueingConsumer(channel);
            var receiverTag = channel.BasicConsume(indirectReplyToQueueName, true, receiver);

            channel.BasicPublish(String.Empty, indirectReplyToQueueName, true, new BasicProperties { ReplyTo = RPCStrategyHelpers.DIRECT_REPLY_TO_QUEUENAME_ARG }, new byte[0]);

            BasicDeliverEventArgs delivery;
            using (ManualResetEventSlim messageReturned = new ManualResetEventSlim())
            {
                EventHandler<BasicReturnEventArgs> returnHandler = null;
                Interlocked.Exchange(ref returnHandler, (a, e) => { messageReturned.Set(); try { receiver.Model.BasicReturn -= returnHandler; } catch { } });
                receiver.Model.BasicReturn += returnHandler;

                System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
                watch.Start();
                while (!receiver.TryInstantDequeue(out delivery))
                {
                    Thread.Sleep(1);
                    if (watch.Elapsed > TimeSpan.FromSeconds(10) || messageReturned.IsSet)
                    {
                        break;
                    }
                }
                watch.Stop();

                if (!messageReturned.IsSet)
                {
                    try
                    {
                        receiver.Model.BasicReturn -= returnHandler;
                    }
                    catch { }
                }

                try
                {
                    channel.BasicCancel(receiverTag);
                }
                catch { }
            }

            if (delivery == null)
            {
                throw new InvalidOperationException("Unable to determine direct reply-to queue name.");
            }

            var result = delivery.BasicProperties.ReplyTo;
            if (result == null || result == RPCStrategyHelpers.DIRECT_REPLY_TO_QUEUENAME_ARG || !result.StartsWith(RPCStrategyHelpers.DIRECT_REPLY_TO_QUEUENAME_ARG))
            {
                throw new InvalidOperationException("Discovered direct reply-to queue name (" + (result ?? "null") + ") was not in expected format.");
            }

            return result;
        }

        public void Dispose()
        {
            disposedCancellationSource.Cancel();

            responseQueued.Dispose();
            disposedCancellationSource.Dispose();
        }
    }
}
