
using System;

namespace RestBus.Common
{
    public class Shared
    {
        public const string SUBSCRIBER_ID_HEADER = "X-RestBus-Subscriber-Id";
        public const string REDELIVERED_HEADER = "X-RestBus-Redelivered";

        static IFormatProvider dateTimeProvider = System.Globalization.CultureInfo.InvariantCulture;

        /// <summary>
        /// Verifies that the specified date string is in RFC 1123 format
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        public static bool IsValidHttpDate(string date)
        {
            DateTime result;
            return DateTime.TryParseExact(date, "r", dateTimeProvider, System.Globalization.DateTimeStyles.None, out result);
        }
    }
}
