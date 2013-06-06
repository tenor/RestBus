using RestBus.RabbitMQ.Common;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;

namespace RestBus.RabbitMQ
{
    //TODO: This should be an internal class too.
    //TODO: Describe why this class exists
    public class HttpRequestPacket
    {
        public byte[] Content;
        public Dictionary<string, IEnumerable<string>> Headers;
        public string Method;
        public string Version;
        public string Resource;


        public HttpRequestPacket()
        {
        }

        public HttpRequestPacket(HttpRequestMessage request)
        {
            Headers = new Dictionary<string, IEnumerable<string>>();
            foreach (var hdr in request.Headers)
            {
                this.Headers.Add(hdr.Key, hdr.Value);
            }

            if (request.Content != null)
            {
                foreach (var hdr in request.Content.Headers)
                {
                    this.Headers.Add(hdr.Key, hdr.Value);
                }
            }

            this.Method = request.Method.Method;
            this.Version = request.Version.ToString();
            this.Resource = request.RequestUri.IsAbsoluteUri ? request.RequestUri.PathAndQuery : request.RequestUri.OriginalString;

            if (request.Content != null)
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    request.Content.CopyToAsync(ms).Wait();
                    Content = ms.ToArray();
                }
            }

        }

    }
}
