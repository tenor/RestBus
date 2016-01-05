using Microsoft.AspNet.Http.Features;
using Microsoft.AspNet.Http.Internal;
using RestBus.Common;
using System;
using System.IO;
using System.Linq;

namespace RestBus.AspNet
{
    internal static class MessageHelpers
    {
        const string HTTP_RESPONSE_VERSION = "1.1";
        readonly static string[] HTTP_RESPONSE_SERVER_HEADER = new string[] { "RestBus.AspNet" };


        internal static bool TryGetServiceMessage (this HttpRequestPacket request, out ServiceMessage message)
        {
            if (request == null) throw new ArgumentNullException("request");

            message = new ServiceMessage();

            //Build Request
            IHttpRequestFeature req = message as IHttpRequestFeature;

            Uri uri;
            try
            {
                uri = request.BuildUri(null, null);
            }
            catch
            {
                message = null;
                return false;
            }
            req.Path = uri.AbsolutePath;

            req.Protocol = "HTTP/" + request.Version;
            req.QueryString = uri.Query;
            req.Method = request.Method;

            if (request.Content != null && request.Content.Length > 0)
            {
                message.CreateRequestBody(request.Content);
            }

            //Add Request Headers
            {
                var headers = new HeaderDictionary();

                foreach (var hdr in request.Headers)
                {
                    if (hdr.Key != null && hdr.Key.Trim().ToUpperInvariant() == "CONTENT-LENGTH") continue; // Content-length is calculated based on actual content.

                    //NOTE: Client already folds Request Headers into RequestPacket, so there's no need to fold it again here.
                    headers.Add(hdr.Key, hdr.Value.ToArray());
                }

                if (message.OriginalRequestBody != null)
                {
                    headers.Add("Content-Length", request.Content.Length.ToString());
                }
                req.Headers = headers;
            }


            //Create Response
            message.CreateResponseBody();
            IHttpResponseFeature resp = message as IHttpResponseFeature;
            resp.StatusCode = 200;

            //Add Response Headers
            {
                var headers = new HeaderDictionary();

                headers.Add("Server", HTTP_RESPONSE_SERVER_HEADER[0]);
                resp.Headers = headers;
            }

            return true;

        }

        internal static HttpResponsePacket ToHttpResponsePacket(this ServiceMessage message)
        {
            if (message == null) throw new ArgumentNullException("message");

            var response = new HttpResponsePacket();

            var respFeature = message as IHttpResponseFeature;
            if (respFeature.Headers != null)
            {
                foreach (var hdr in respFeature.Headers)
                {
                    response.Headers.Add(hdr.Key, hdr.Value);
                }
            }

            response.Version = HTTP_RESPONSE_VERSION;
            response.StatusCode = respFeature.StatusCode;
            if(String.IsNullOrEmpty(respFeature.ReasonPhrase))
            {
                response.StatusDescription = ReasonPhrases.ToReasonPhrase(response.StatusCode) ?? "Unknown";
            }
            else
            {
                response.StatusDescription = respFeature.ReasonPhrase;
            }
            
            if (message.OriginalResponseBody != null && message.OriginalResponseBody.CanRead)
            {
                //NOTE: OriginalResponseBody.CanRead will be false if the stream was disposed.

                response.Content = message.OriginalResponseBody.ToArray();
            }

            //Add/Update Server header
            response.Headers["Server"] = HTTP_RESPONSE_SERVER_HEADER;

            return response;
        }
    }
}
