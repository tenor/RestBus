using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RestBus.Common;
using RestBus.Common.Http;
using RestBus.RabbitMQ.ChannelPooling;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace RestBus.RabbitMQ.Client
{
    internal class RPCStrategyHelpers
    {
        internal const string DIRECT_REPLY_TO_QUEUENAME_ARG = "amq.rabbitmq.reply-to";
        internal const int HEART_BEAT = 30;

        internal static void WaitForResponse (HttpRequestMessage request, ExpectedResponse arrival, TimeSpan requestTimeout, TaskCompletionSource<HttpResponseMessage> taskSource, Action cleanup)
        {
            //Spawning a new task to wait on the MRESlim is slower than using ThreadPool.RegisterWaitForSingleObject
            //
            //TODO: Test task vs RegisterWaitForSingleObject modes in a super fast network environment with 40, 100, 200 all the way to 1000 threads to see what method has fastest throughput.

#if WAIT_FOR_RESPONSE_IN_TASK_MODE

                    //Wait for response arrival event on a new task

                    Task.Factory.StartNew(() =>
                    {
                        bool succeeded = arrival.ReceivedEvent.Wait(
                            requestTimeout.TotalMilliseconds > Int32.MaxValue ? TimeSpan.FromMilliseconds(Int32.MaxValue) : requestTimeout /* Covers InfiniteTimeSpan */,
                            cancellationToken);

                        Thread.MemoryBarrier(); //Ensure non-cached versions of arrival are read

                        //It failed either because it timed out or was cancelled
                        //HttpClient treats both scenarios the same way.

                        try
                        {
                            SetResponseResult(request, !succeeded, arrival, taskSource);
                        }
                        catch
                        {
                            //TODO: Log this: 
                            // the code in the try block should be safe so this catch block should never be called, 
                            // hoewever, this delegate is called on a separate thread and should be protected.
                        }
                        finally
                        {
                            CleanupMessagingResources(correlationId, arrival);
                        }

                    }, cancellationToken);

#else

            //Wait for response arrival event on the ThreadPool:

            var localVariableInitLock = new object();

            lock (localVariableInitLock)
            {
                //TODO: Have cancellationToken signal WaitHandle so that threadpool stops waiting.

                RegisteredWaitHandle callbackHandle = null;
                callbackHandle = ThreadPool.RegisterWaitForSingleObject(arrival.ReceivedEvent.WaitHandle,
                    (state, timedOut) =>
                    {
                        //TODO: Investigate, this memorybarrier might be unnecessary since the thread is released from the threadpool
                        //after deserializationException and responsePacket is set.
                        Thread.MemoryBarrier(); //Ensure non-cached versions of arrival are read
                        try
                        {
                            //TODO: Check Cancelation Token when it's implemented

                            SetResponseResult(request, timedOut, arrival, taskSource);

                            lock (localVariableInitLock)
                            {
                                callbackHandle.Unregister(null);
                            }
                        }
                        catch
                        {
                            //TODO: Log this: 
                            // the code in the try block should be safe so this catch block should never be called, 
                            // hoewever, this delegate is called on a separate thread and should be protected.
                        }
                        finally
                        {
                            cleanup();
                        }
                    },
                        null,
                        requestTimeout == System.Threading.Timeout.InfiniteTimeSpan ? System.Threading.Timeout.Infinite : (long)requestTimeout.TotalMilliseconds,
                        true);

            }
#endif
        }

        internal static void ReadAndSignalDelivery (ExpectedResponse expected, BasicDeliverEventArgs evt)
        {
            try
            {
                expected.Response = HttpResponsePacket.Deserialize(evt.Body);
            }
            catch (Exception ex)
            {
                expected.DeserializationException = ex;
            }

            //NOTE: The ManualResetEventSlim.Set() method can be called after the object has been disposed
            //So no worries about the Timeout disposing the object before the response comes in.
            expected.ReceivedEvent.Set();
        }

        internal static void CreateNewConnectionAndChannelPool(ConnectionFactory connectionFactory, ref IConnection clientConn, ref AmqpChannelPooler clientPool, out IConnection newConn, out AmqpChannelPooler newPool)
        {
            //NOTE: This is the only place where connections are created in the client
            //NOTE: CreateConnection() can always throw RabbitMQ.Client.Exceptions.BrokerUnreachableException
            newConn = connectionFactory.CreateConnection();

            //Swap out client connection and pooler, so other threads can use the new objects:

            //First Swap out old pool with new pool
            newPool = new AmqpChannelPooler(newConn);
            var oldpool = Interlocked.Exchange(ref clientPool, newPool);

            //then swap out old connection with new one
            var oldconn = Interlocked.Exchange(ref clientConn, newConn);

            //Dispose old pool
            if (oldpool != null)
            {
                oldpool.Dispose();
            }

            //Dispose old connection
            DisposeConnection(oldconn);
        }

        internal static void ConnectToServer(object syncObj, ConnectionFactory connectionFactory, ref IConnection clientConn, ref AmqpChannelPooler clientPool)
        {
            if (clientConn == null || !clientConn.IsOpen)
            {
                //TODO: Can double-checked locking here be simplified?

                lock (syncObj)
                {
                    if (clientConn == null || !clientConn.IsOpen)
                    {
                        IConnection newConn;
                        AmqpChannelPooler newPool;
                        CreateNewConnectionAndChannelPool(connectionFactory, ref clientConn, ref clientPool, out newConn, out newPool);
                    }
                }
            }
        }


        internal static void DisposeConnection(IConnection connection)
        {
            if (connection != null)
            {

                try
                {
                    connection.Close();
                }
                catch
                {
                    //TODO: Log Error
                }

                try
                {
                    connection.Dispose();
                }
                catch
                {
                    //TODO: Log Error
                }
            }
        }

        private static void SetResponseResult(HttpRequestMessage request, bool timedOut, ExpectedResponse arrival, TaskCompletionSource<HttpResponseMessage> taskSource)
        {
            if (timedOut)
            {
                //NOTE: This really ought to return an "Operation Timed Out" WebException and not a Cancellation as noted in the following posts
                // http://social.msdn.microsoft.com/Forums/en-US/d8d87789-0ac9-4294-84a0-91c9fa27e353/bug-in-httpclientgetasync-should-throw-webexception-not-taskcanceledexception?forum=netfxnetcom&prof=required
                // http://stackoverflow.com/questions/10547895/how-can-i-tell-when-httpclient-has-timed-out
                // and http://stackoverflow.com/questions/12666922/distinguish-timeout-from-user-cancellation
                //
                // However, for compatibility with the HttpClient, it returns a cancellation
                //

                taskSource.SetCanceled();
            }
            else
            {
                if (arrival.DeserializationException == null)
                {
                    var res = arrival.Response;
                    if (res != null)
                    {
                        //Add/Update Content-Length Header
                        //TODO: Is there any need to add this here if it's subsequently removed/updated by TryGetHttpResponseMessage/HttpPacket.PopulateHeaders? (Is this useful in the exception/other path scenario?
                        res.Headers["Content-Length"] = new string[] { (res.Content == null ? 0 : res.Content.Length).ToString() }; ;
                    }
                    else
                    {
                        //TODO: Log this -- Critical issue (or just assert)
                    }
                }

                HttpResponseMessage msg;
                if (arrival.DeserializationException == null && TryGetHttpResponseMessage(arrival.Response, out msg))
                {
                    msg.RequestMessage = request;
                    taskSource.SetResult(msg);
                }
                else
                {
                    taskSource.SetException(RestBusClient.GetWrappedException("An error occurred while reading the response.", arrival.DeserializationException));
                }
            }
        }

        private static bool TryGetHttpResponseMessage(HttpResponsePacket packet, out HttpResponseMessage response)
        {
            try
            {
                response = new HttpResponseMessage
                {
                    Content = new ByteArrayContent(packet.Content ?? new byte[0]),
                    Version = new Version(packet.Version),
                    ReasonPhrase = packet.StatusDescription,
                    StatusCode = (System.Net.HttpStatusCode)packet.StatusCode
                };

                packet.PopulateHeaders(response.Content.Headers, response.Headers);
            }
            catch
            {
                response = null;
                return false;
            }

            return true;
        }


    }
}
