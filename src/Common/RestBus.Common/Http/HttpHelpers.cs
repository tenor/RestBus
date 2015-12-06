using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;

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

                if (hdrKey == "CONTENT-LENGTH") continue; //Content Length is automatically calculated

                if (Array.IndexOf<String>(contentOnlyHeaders, hdrKey) >= 0)
                {
                    contentHeaders.Add(hdr.Key.Trim(), hdr.Value);
                }
                else
                {
                    generalHeaders.Add(hdr.Key.Trim(), hdr.Value);
                }
            }
        }

        #endregion

        #region Helper Methods to create new HttpRequestPacket / HttpResponsePackets from HttpRequestMessage / HttpResponseMessage
        public static HttpRequestPacket ToHttpRequestPacket (this HttpRequestMessage request)
        {
            var packet = new HttpRequestPacket();

            foreach (var hdr in request.Headers)
            {
                packet.AddHeader(hdr);
            }

            if (request.Content != null)
            {
                foreach (var hdr in request.Content.Headers)
                {
                    packet.AddHeader(hdr);
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
                packet.AddHeader(hdr);
            }

            if (response.Content != null)
            {
                foreach (var hdr in response.Content.Headers)
                {
                    packet.AddHeader(hdr);
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

        ///<summary>Adds a header to the request packet. </summary>
        ///<remarks>NOTE: This method folds the headers as expected in WebAPI 2's Request.Header object.</remarks>
        private static void AddHeader(this HttpRequestPacket packet, KeyValuePair<string, IEnumerable<string>> hdr)
        {
            if (packet == null) throw new ArgumentNullException("packet");

            if (packet.Headers.ContainsKey(hdr.Key))
            {
                var headerValue = ((List<string>)packet.Headers[hdr.Key])[0];
                if(String.IsNullOrEmpty(headerValue))
                {
                    headerValue = String.Join(", ", hdr.Value);
                }
                else
                {
                    headerValue = headerValue + ", " + String.Join(", ", hdr.Value);
                }
                ((List<string>)packet.Headers[hdr.Key])[0] = headerValue;
            }
            else
            {
                packet.Headers.Add(hdr.Key, new List<string>() { String.Join(", ", hdr.Value) });
            }
        }

        ///<summary>Adds a header to the response packet. </summary>
        ///<remarks>NOTE: This method does not fold the headers as expected in WebAPI 2's response stream.</remarks> 
        private static void AddHeader(this HttpResponsePacket packet, KeyValuePair<string, IEnumerable<string>> hdr)
        {
            if (packet == null) throw new ArgumentNullException("packet");

            if (packet.Headers.ContainsKey(hdr.Key))
            {
                ((List<string>)packet.Headers[hdr.Key]).AddRange(hdr.Value);
            }
            else
            {
                packet.Headers.Add(hdr.Key, new List<string>(hdr.Value));
            }
        }

        #endregion

    }
}
