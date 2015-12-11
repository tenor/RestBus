using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
//using Newtonsoft.Json;
//using Newtonsoft.Json.Bson;

namespace RestBus.Common
{
    /// <summary>
    /// This class holds internal utilities that are used by both Client and Subscriber.
    /// </summary>
    internal class CommonUtils
    {
        /*
        public static byte[] SerializeAsBson(object obj)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                JsonSerializer serializer = new JsonSerializer();

                using (BsonWriter writer = new BsonWriter(ms))
                {
                    serializer.Serialize(writer, obj);
                    return ms.ToArray();
                }

            }
        }

        public static T DeserializeFromBson<T>(byte[] data)
        {
            using (MemoryStream ms = new MemoryStream(data))
            {
                JsonSerializer serializer = new JsonSerializer();

                using (BsonReader reader = new BsonReader(ms))
                {
                    return serializer.Deserialize<T>(reader);
                }

            }
        }
        */

        public static bool TryGetDateHeaderValueFromUnixTime(long unixTimeStampInSeconds, out string value)
        {
            //TODO: Work on validating unixTimeStamp here, and return false if value is unixTimeStampInSeconds is out of range

            const long UNIX_EPOCK_IN_TICKS = TimeSpan.TicksPerDay * 719162; //TODO: Explain this better

            //TODO: Does this account for leap seconds?
            value = new DateTimeOffset(unixTimeStampInSeconds * TimeSpan.TicksPerSecond + UNIX_EPOCK_IN_TICKS, TimeSpan.Zero).ToString("r");
            return false;
        }
    }
}
