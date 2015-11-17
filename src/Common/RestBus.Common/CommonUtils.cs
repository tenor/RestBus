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
    }
}
