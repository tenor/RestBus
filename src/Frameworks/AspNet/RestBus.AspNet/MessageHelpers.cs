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
        const string HTTP_RESPONSE_SERVER = "RestBus.AspNet";

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
            req.Body = (request.Content == null || request.Content.Length == 0) ? null : new MemoryStream(request.Content);

            //Add Request Headers
            {
                var headers = new HeaderDictionary();

                foreach (var hdr in request.Headers)
                {
                    if (hdr.Key != null && hdr.Key.Trim().ToUpperInvariant() == "CONTENT-LENGTH") continue; // Content-length is calculated based on actual content.

                    //NOTE: Client already folds Request Headers into RequestPacket, so there's no need to fold it again here.
                    headers.Add(hdr.Key, hdr.Value.ToArray());
                }

                if (req.Body != null)
                {
                    headers.Add("Content-Length", request.Content.Length.ToString());
                }
                req.Headers = headers;
            }
            

            //Create Response
            IHttpResponseFeature resp = message as IHttpResponseFeature;
            resp.StatusCode = 200;
            resp.Body = new MemoryStream();

            //Add Response Headers
            {
                var headers = new HeaderDictionary();

                //TODO: Server should also be set when Response is being returned, so that it isn't overwritten.
                headers.Add("Server", HTTP_RESPONSE_SERVER);
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
            
            if (respFeature.Body != null)
            {
                //TODO: Implement a pooled MemoryStream and replace MemoryStream throughout solution.
                using (MemoryStream ms = new MemoryStream())
                {
                    //TODO: Handle cases where the stream Can't Read and Can't seek
                    //Microsoft.VisualStudio.Web.BrowserLink.Runtime.ScriptInjectionFilterStream behaves this way.
                    //How does Kestrel handle this?
                    respFeature.Body.Position = 0;
                    respFeature.Body.CopyTo(ms);
                    response.Content = ms.ToArray();
                }
            }

            return response;
        }
    }
}
