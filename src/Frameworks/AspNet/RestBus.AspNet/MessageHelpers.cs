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

                //TODO: Something to think about: Should host add Date response header.
                //If so, shouldn't that be done at the time that the response is been transmitted
                //In which case, the subscriber should be responsible for adding/overwriting that value.
                //In any case, remember DateTime.UtcNow/Now is very slow, so use Environment.TickCount in addition to some
                //other mechanism that limits the number of times, DateTime.UtcNow is polled.
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
            response.StatusDescription = respFeature.ReasonPhrase;

            if (respFeature.Body != null)
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    respFeature.Body.Position = 0;
                    respFeature.Body.CopyTo(ms);
                    response.Content = ms.ToArray();
                }
            }

            return response;
        }
    }
}
