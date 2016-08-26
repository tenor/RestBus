using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using RestBus.Common;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace RestBus.AspNet
{

    //TODO: Describe what this class does
    internal class RestBusHost<TContext>
        : IDisposable
    {

        private readonly IRestBusSubscriber subscriber;
        private readonly IHttpApplication<TContext> application;
        private readonly IApplicationLifetime applicationLifetime;
        private readonly ILogger logger;

        private InterlockedBoolean hasStarted;
        private volatile bool disposed;

        /// <summary>
        /// Initializes a new instance of <see cref="RestBusHost{TContext}"/>
        /// </summary>
        /// <param name="subscriber">The RestBus Subscriber</param>
        /// <param name="application">The HttpApplication</param>
        internal RestBusHost(IRestBusSubscriber subscriber, IHttpApplication<TContext> application, IApplicationLifetime applicationLifetime, ILoggerFactory logFactory)
        {
            this.subscriber = subscriber;
            this.application = application;
            this.applicationLifetime = applicationLifetime;
            this.logger = logFactory.CreateLogger<RestBusHost<TContext>>();
        }

        /// <summary>
        /// Starts the host
        /// </summary>
        internal void Start()
        {
            if (!hasStarted.SetTrueIf(false))
            {
                throw new InvalidOperationException("RestBus host has already started!");
            }

            this.logger.LogInformation($"{nameof(RestBusHost<TContext>)} is starting.");
            subscriber.Start();

            Task.Factory.StartNew(RunLoop, creationOptions: TaskCreationOptions.LongRunning);
        }

        /// <summary>
        /// Disposes the host
        /// </summary>
        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                subscriber.Dispose();
            }
        }

        /// <summary>
        /// Main loop which dequeues requests and spawns new tasks to process them.
        /// </summary>
        private void RunLoop()
        {
            MessageContext context = null;
            while (true)
            {
                try
                {
                    if (!this.applicationLifetime.ApplicationStopping.IsCancellationRequested)
                    {
                        this.logger.LogInformation($"Calling {nameof(subscriber.Dequeue)}.");
                        context = subscriber.Dequeue();
                    }
                    else
                    {
                        this.logger.LogInformation($"An ApplicationStopping cancellation is requested by {applicationLifetime.GetType().FullName}.");
                        break;
                    }
                }
                catch (Exception e)
                {
                    if (!(e is ObjectDisposedException || e is OperationCanceledException))
                    {
                        //TODO: Log exception: Don't know what else to expect here
                        this.logger.LogError(e.Message + Environment.NewLine + e.StackTrace);
                    }

                    //Exit method if host has been disposed
                    if (disposed)
                    {
                        break;
                    }
                    else
                    {
                        continue;
                    }
                }

                var cancellationToken = CancellationToken.None;
                this.logger.LogInformation($"Starting a new process for {context.CorrelationId}.");
                Task.Factory.StartNew((Func<object, Task>)Process, Tuple.Create(context, cancellationToken), cancellationToken);
            }
        }

        /// <summary>
        /// Processes a dequeued request 
        /// </summary>
        /// <remarks>
        /// This method runs in a new Task.
        /// </remarks>
        private async Task Process(object state)
        {
            try
            {
                var typedState = (Tuple<MessageContext, CancellationToken>)state;
                await ProcessRequest(typedState.Item1, typedState.Item2);
            }
            catch (Exception ex)
            {
                //TODO: Shouldn't occur (the called method should be safe): Log execption and return a server error
                this.logger.LogError(ex.Message + Environment.NewLine + ex.StackTrace);
            }
        }

        /// <summary>
        /// Processes a request.
        /// </summary>
        private async Task ProcessRequest(MessageContext restbusContext, CancellationToken cancellationToken)
        {
            //NOTE: This method is called on a background thread and must be protected by an outer big-try catch

            TContext context = default(TContext);
            Exception appException = null;
            ServiceMessage msg = null;
            bool appInvoked = false;

            try
            {
                if (disposed)
                {
                    msg = CreateResponse(HttpStatusCode.ServiceUnavailable, "The server is no longer available.");
                }
                else
                {
                    if (!restbusContext.Request.TryGetServiceMessage(out msg))
                    {
                        //Bad message
                        msg = CreateResponse(HttpStatusCode.BadRequest, "Bad Request");
                    }
                    else
                    {
                        context = application.CreateContext(msg);

                        //Call application
                        appInvoked = true;
                        try
                        {
                            await application.ProcessRequestAsync(context).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            ReportApplicationError(msg, ex);
                        }
                        finally
                        {
                            //Call FireOnResponseStarting if message didn't encounter an exception.
                            if (!msg.HasApplicationException)
                            {
                                await FireOnResponseStarting(msg).ConfigureAwait(false);
                            }
                        }
                    }

                }

                if (msg.HasApplicationException)
                {
                    appException = msg._applicationException;

                    //If request encountered an exception then return an Internal Server Error (with empty body) response.
                    msg.Dispose();
                    msg = CreateResponse(HttpStatusCode.InternalServerError, null);
                }


                //Send Response
                HttpResponsePacket responsePkt;

                try
                {
                    responsePkt = msg.ToHttpResponsePacket();
                }
                catch (Exception ex)
                {
                    responsePkt = CreateResponseFromException(ex).ToHttpResponsePacket();
                }

                try
                {
                    subscriber.SendResponse(restbusContext, responsePkt);
                }
                catch (Exception ex)
                {
                    //TODO: Log SendResponse error
                    this.logger.LogError(ex.Message + Environment.NewLine + ex.StackTrace);
                }

                //Call OnCompleted callbacks
                if (appInvoked)
                {
                    await FireOnResponseCompleted(msg).ConfigureAwait(false);
                }
            }
            finally
            {
                if (appInvoked)
                {
                    application.DisposeContext(context, appException);
                }

                if (msg != null)
                {
                    msg.Dispose();
                }
            }

        }

        #region Helpers
        private static ServiceMessage CreateResponse(HttpStatusCode status, string reasonPhrase, string body = null)
        {
            var msg = new ServiceMessage();
            ((IHttpResponseFeature)msg).StatusCode = (int)status;
            ((IHttpResponseFeature)msg).ReasonPhrase = reasonPhrase;

            if (body != null)
            {
                msg.CreateResponseBody();
                var buffer = System.Text.Encoding.UTF8.GetBytes(body);
                msg.OriginalResponseBody.Write(buffer, 0, buffer.Length);
            }

            return msg;
        }

        private ServiceMessage CreateResponseFromException(Exception ex)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("Exception: \r\n\r\n");
            sb.Append(ex.Message);
            sb.Append("\r\n\r\nStackTrace: \r\n\r\n");
            sb.Append(ex.StackTrace);

            if (ex.InnerException != null)
            {
                sb.Append("Inner Exception: \r\n\r\n");
                sb.Append(ex.InnerException.Message);
                sb.Append("\r\n\r\nStackTrace: \r\n\r\n");
                sb.Append(ex.InnerException.StackTrace);
            }

            return CreateResponse(HttpStatusCode.InternalServerError, "An unexpected exception was thrown.", body: sb.ToString());
        }

        private async Task FireOnResponseStarting(ServiceMessage msg)
        {
            var onStarting = msg._onStarting;
            if (onStarting != null)
            {
                try
                {
                    foreach (var entry in onStarting)
                    {
                        await entry.Key.Invoke(entry.Value);
                    }
                }
                catch (Exception ex)
                {
                    ReportApplicationError(msg, ex);
                }
            }
        }

        private async Task FireOnResponseCompleted(ServiceMessage msg)
        {
            var onCompleted = msg._onCompleted;
            if (onCompleted != null)
            {
                foreach (var entry in onCompleted)
                {
                    try
                    {
                        await entry.Key.Invoke(entry.Value);
                    }
                    catch (Exception ex)
                    {
                        ReportApplicationError(msg, ex);
                    }
                }
            }
        }

        private void ReportApplicationError(ServiceMessage msg, Exception ex)
        {
            msg._applicationException = ex;
            //TODO: Log Application error
            this.logger.LogError(ex.Message + Environment.NewLine + ex.StackTrace);
        }
        #endregion
    }
}
