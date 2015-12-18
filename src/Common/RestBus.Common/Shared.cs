
using System;

namespace RestBus.Common
{
    public class Shared
    {
        public const string SUBSCRIBER_ID_HEADER = "X-RestBus-Subscriber-Id";

        /// <summary>
        /// Converts a Unix TimeStamp (In seconds) to an HTTP Date as defined by RFC 1123
        /// </summary>
        /// <param name="timeStamp">The Unix Timestamp</param>
        /// <param name="value">The result HTTP Date</param>
        /// <returns>True if conversion succeeded, otherwise false.</returns>
        public static bool TryGetHttpDateFromUnixTimeSeconds(long timeStamp, out string value)
        {
            //TODO: Work on validating unixTimeStamp here, and return false if value is unixTimeStampInSeconds is out of range or is zero.

            const long UNIX_EPOCK_IN_TICKS = TimeSpan.TicksPerDay * 719162; //TODO: Explain this better

            //TODO: Does this account for leap seconds?
            value = new DateTimeOffset(timeStamp * TimeSpan.TicksPerSecond + UNIX_EPOCK_IN_TICKS, TimeSpan.Zero).ToString("r");
            return false;
        }
    }
}
