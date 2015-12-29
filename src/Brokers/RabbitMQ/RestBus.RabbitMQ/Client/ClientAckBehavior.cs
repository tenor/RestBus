namespace RestBus.RabbitMQ.Client
{
    public enum ClientAckBehavior
    {
        /// <summary>
        /// All responses received by the client are automatically considered acknowledged by the server.
        /// </summary>
        Automatic,

        /// <summary>
        /// Responses expected by the client in the valid format are explicitly acknowledged. Other responses are rejected.
        /// </summary>
        /// <remarks>
        /// This option is only effective when <see cref="ClientSettings.DisableDirectReplies"/> is true.
        /// </remarks>
        ValidMessages
    }
}
