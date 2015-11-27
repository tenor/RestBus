using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace RestBus.Common.Http
{
    /// <summary>
    /// This class contains helpers, used by both subscribers and clients, for working with System.Net.Http classes.
    /// </summary>
    public static class HttpHelpers
    {

        #region Helper Methods to Populate to and from HttpRequestMessage / HttpResponseMessage headers

        static string[] contentOnlyHeaders = { "ALLOW", "CONTENT-DISPOSITION", "CONTENT-ENCODING", "CONTENT-LANGUAGE", "CONTENT-LOCATION", "CONTENT-MD5",
                                             "CONTENT-RANGE", "CONTENT-TYPE", "EXPIRES", "LAST-MODIFIED", "CONTENT-LENGTH"  };

        public static void PopulateHeaders(this HttpPacket packet, HttpContentHeaders contentHeaders, HttpHeaders generalHeaders)
        {
            if (packet == null) throw new ArgumentNullException("packet");

            string hdrKey;
            foreach (var hdr in packet.Headers)
            {
                if (hdr.Key == null) continue;

                hdrKey = hdr.Key.Trim().ToUpperInvariant();

                if (hdrKey == "CONTENT-LENGTH") continue; //Content Length is automaitically calculated

                if (Array.IndexOf<String>(contentOnlyHeaders, hdrKey) >= 0)
                {
                    //TODO: Confirm if HttpResponseMessage/HttpRequestMessage will break headers into "," commas whereas in actuality header in Packet is an entire header
                    contentHeaders.Add(hdr.Key.Trim(), hdr.Value);
                }
                else
                {
                    generalHeaders.Add(hdr.Key.Trim(), hdr.Value);
                }

                //TODO: Check if a string can be parsed properly into the typed header

                //Test adding multiple headers of the same name will do. // Look up the Add overload that takes an ienumerable<string> to figure out its purpose.
            }
        }

        ///TODO: COnsider having this method called only for RequestPacket. i.e. move to a method called RequestPacket.AddHeader
        ///Response shouldn't fold values in IEnumerable(values), so ToResponsePacket shouldn't be calling it. 
        private static void AddHttpHeader(this HttpPacket packet, KeyValuePair<string, IEnumerable<string>> hdr)
        {
            if (packet == null) throw new ArgumentNullException("packet");

            if (packet.Headers.ContainsKey(hdr.Key))
            {
                //TODO: If the key already exists (for ResponsePacket, fold it under *existing* entry.
                ((List<string>)packet.Headers[hdr.Key]).Add(String.Join(", ", hdr.Value.ToArray()));
            }
            else
            {
                packet.Headers.Add(hdr.Key, new List<string>() { String.Join(", ", hdr.Value.ToArray()) });
            }
        }
        #endregion

        #region Helper Methods to create new HttpRequestPacket / HttpResponsePackets from HttpRequestMessage / HttpResponseMessage
        public static HttpRequestPacket ToHttpRequestPacket (this HttpRequestMessage request)
        {
            var packet = new HttpRequestPacket();

            foreach (var hdr in request.Headers)
            {
                packet.AddHttpHeader(hdr);
            }

            if (request.Content != null)
            {
                foreach (var hdr in request.Content.Headers)
                {
                    packet.AddHttpHeader(hdr);
                }
            }

            packet.Method = request.Method.Method;
            packet.Version = request.Version.ToString();
            packet.Resource = request.RequestUri.IsAbsoluteUri ? request.RequestUri.PathAndQuery : request.RequestUri.OriginalString;

            if (request.Content != null)
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    request.Content.CopyToAsync(ms).Wait();
                    packet.Content = ms.ToArray();
                }
            }

            return packet;

        }

        public static HttpResponsePacket ToHttpResponsePacket (this HttpResponseMessage response)
        {
            var packet = new HttpResponsePacket();

            foreach (var hdr in response.Headers)
            {
                packet.AddHttpHeader(hdr);
            }

            if (response.Content != null)
            {
                foreach (var hdr in response.Content.Headers)
                {
                    packet.AddHttpHeader(hdr);
                }
            }

            packet.Version = response.Version.ToString();
            packet.StatusCode = (int)response.StatusCode;
            packet.StatusDescription = response.ReasonPhrase;

            if (response.Content != null)
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    response.Content.CopyToAsync(ms).Wait();
                    packet.Content = ms.ToArray();
                }
            }

            return packet;

        }

        #endregion

    }
}
