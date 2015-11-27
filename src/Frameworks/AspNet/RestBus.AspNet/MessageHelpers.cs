using Microsoft.AspNet.Http;
using Microsoft.AspNet.Http.Features;
using Microsoft.AspNet.Http.Internal;
using RestBus.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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
            req.Body = new MemoryStream(request.Content);

            //Add Request Headers
            {
                var headers = new HeaderDictionary();

                foreach (var hdr in request.Headers)
                {
                    //NOTE: Client already folds Request Headers into RequestPacket, so there's no need to fold it again here.
                    headers.Add(hdr.Key, hdr.Value.ToArray());
                }
                req.Headers = headers;
            }
            

            //Create Response
            IHttpResponseFeature resp = message as IHttpResponseFeature;
            resp.StatusCode = 200;

            //Add Response Headers
            resp.Body = new MemoryStream();
            {
                var headers = new HeaderDictionary();

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
