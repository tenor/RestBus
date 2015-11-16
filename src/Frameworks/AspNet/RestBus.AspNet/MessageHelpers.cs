using Microsoft.AspNet.Http;
using Microsoft.AspNet.Http.Features;
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
        internal static ServiceMessage ToServiceMessage (this HttpRequestPacket request)
        {
            if (request == null) throw new ArgumentNullException("request");

            var message = new ServiceMessage();
            IHttpRequestFeature reqRes = message as IHttpRequestFeature;
            if(request.Resource != null)
            {
                //TODO: Is QueryString Part of result.Path or is it clipped
                //Should probably use GetUriFromResource() method to Parse Path, QueryString, PathBase AND PROTOCOL properly
                if (request.Resource.StartsWith("/"))
                {
                    reqRes.Path = request.Resource;
                }
                else
                {
                    reqRes.Path = "/" + request.Resource;
                }
            }
            reqRes.Protocol = "HTTP/1.1"; //TODO: Change this
            reqRes.QueryString = ""; //TODO: Get QueryString properly, 
            {
                var headers = new Dictionary<string, Microsoft.Framework.Primitives.StringValues>();

                foreach (var hdr in request.Headers)
                {
                    headers.Add(hdr.Key, new Microsoft.Framework.Primitives.StringValues(hdr.Value.ToArray()));
                }
                reqRes.Headers = headers;
            }
            
            reqRes.Method = request.Method;
            reqRes.Body = new MemoryStream(request.Content);

            //Response
            IHttpResponseFeature resp = message as IHttpResponseFeature;
            resp.Body = new MemoryStream(); //TODO: How expensive is a brand new memorystream?
            {
                var headers = new Dictionary<string, Microsoft.Framework.Primitives.StringValues>();

                //foreach (var hdr in request.Headers)
                //{
                //    headers.Add(hdr.Key, new Microsoft.Framework.Primitives.StringValues(hdr.Value.ToArray()));
                //}
                //reqRes.Headers = headers;

                resp.Headers = new Dictionary<string, Microsoft.Framework.Primitives.StringValues>(); //TODO: Change this so it's read off a new response parameter object
            }
            resp.StatusCode = 200;

            return message;

        }

        internal static HttpResponsePacket ToHttpResponse(HttpResponse response, ServiceMessage msg)
        {
            var rsp = new HttpResponsePacket();

            foreach (var hdr in response.Headers)
            {
                // TODO: Fix adding response headers
                //AddHttpHeader(hdr);
            }

            //TODO: Decide if to read mostly from ServiceMessage or from response.

            //rsp.Version = response.... //TODO: Add a default version here
            rsp.StatusCode = (int)response.StatusCode;
            rsp.StatusDescription = ((IHttpResponseFeature)msg).ReasonPhrase;

            if (response.Body != null)
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    response.Body.Position = 0;
                    response.Body.CopyTo(ms);
                    rsp.Content = ms.ToArray();
                }
            }

            return rsp;

        }
    }
}
