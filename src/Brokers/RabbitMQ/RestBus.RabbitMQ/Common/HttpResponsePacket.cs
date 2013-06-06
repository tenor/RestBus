using RestBus.RabbitMQ.Common;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;

namespace RestBus.RabbitMQ
{
    //TODO: This should be an internal class too.
    //This class should just be in RestBus.Common
    //TODO: Describe why this class exists
    public class HttpResponsePacket
    {
        public byte[] Content;
        public Dictionary<string, IEnumerable<string>> Headers = new Dictionary<string,IEnumerable<string>>();
        public string Version;
        public int StatusCode;
        public string StatusDescription;


        public HttpResponsePacket()
        {
        }

    }
}
