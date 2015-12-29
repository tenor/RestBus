namespace RestBus.RabbitMQ.Subscription
{
    public enum SubscriberAckBehavior
    {
        /// <summary>
        /// Requests are explicitly acknowledged after they have been fully processed.
        /// Requests in an unexpected format are rejected.
        /// This is the default behavior.
        /// </summary>
        ProcessedRequests,

        /// <summary>
        /// Requests are automatically acknowledged by the broker once they are received by the subscriber.
        /// </summary>
        Automatic

        /*
        Possible future additions, for consideration, to ack behavior include:

            ImmediateValidRequests -- Request that were deserialized intact and in expected format are explicitly acknowledged before being processed. 
                                      Requests in an unexpected format are rejected.

            ProcessedErrorFree -- Similar to ProcessedRequests, but response results with 5xx status codes are rejected.

            ImmediateNonIdempotent -- Requests with non-idempotent verbs like POST are explicitly acked immediately before being processed, 
                                      whereas idempotent verbs like GET are acked after processing succeeds.
                                      The reasoning here is that is that GET requests can be safely requeued if the subscriber crashes, but
                                      POST requests should be treated with at most once semantics since they can modify state.

            CustomBeforeProcessed -- You supply your own delegate that decides if a request should be acknowledged or rejected before processing
                                     Or decide if to defer acknowledgement to a CustomAfterProcessing delegate.

            CustomAfterProcessed -- You supply your own delegate that decides if a request should be acknowledged or reject after processing is complete.

        */
    }
}
