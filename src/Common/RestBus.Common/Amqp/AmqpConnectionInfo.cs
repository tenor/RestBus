namespace RestBus.Common.Amqp
{
    /// <summary>
    /// Represents a connection string to an AMQP server
    /// </summary>
    public class AmqpConnectionInfo
    {
        /// <summary>
        /// The connection AMQP Uri.
        /// </summary>
        public string Uri { get; set; }

        /// <summary>
        /// The friendly name of this connection, used for logging purposes.
        /// </summary>
        /// <remarks>
        /// The host will display and or log this value, instead of the Uri, which can contain sensitive information such as passwords.
        /// </remarks>
        public string FriendlyName { get; set; }
    }
}
