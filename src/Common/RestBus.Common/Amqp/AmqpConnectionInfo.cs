using System;
using System.Collections.Generic;

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

        public static void EnsureValid(IList<AmqpConnectionInfo> uris, string argumentName)
        {
            //Check uris
            if (uris == null) throw new ArgumentException(argumentName + " is null.");
            if (uris.Count == 0) throw new ArgumentException(argumentName + " must not be empty.");
            for (int i = 0; i < uris.Count; i++)
            {
                if (uris[i] == null) throw new ArgumentException("Index " + i + " of " + argumentName + " argument is null.");
                if (uris[i].Uri == null) throw new ArgumentException("Uri property of Index " + i + " of " + argumentName + " argument is null.");
            }
        }
    }
}
