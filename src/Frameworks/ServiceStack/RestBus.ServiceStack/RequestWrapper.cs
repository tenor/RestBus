using RestBus.Common;
using ServiceStack.ServiceHost;
using ServiceStack.WebHost.Endpoints;
using ServiceStack.WebHost.Endpoints.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RestBus.ServiceStack
{
    public class RequestWrapper : IHttpRequest
    {
        //TODO: Fully implement this class to properly pull out all properties from the Http Message
        //Also cache calculated properties
        //Also look at System.Web.HttpRequest.cs in the MONO project

        private HttpRequestPacket request;

        public RequestWrapper(HttpRequestPacket request)
        {
            //Add Content-Length Header if request has content.
            if (request.Content != null && request.Content.Length > 0)
            {
                request.Headers["Content-Length"] = new string[] { request.Content.Length.ToString() };
            }

            this.request = request;
        }

        public string AbsoluteUri
        {
            get { return request.Resource; }
        }

        public string[] AcceptTypes
        {
            get {
                return GetHeaderValues(request, "Accept").ToArray();
            }
        }

        public string ApplicationFilePath
        {
            get { return string.Empty; }
        }

        public long ContentLength
        {
            get { return request.Content.LongLength; }
        }

        public string ContentType
        {
            get { return GetHeaderValues(request, "Content-Type").FirstOrDefault() ?? String.Empty; }
        }

        public IDictionary<string, System.Net.Cookie> Cookies
        {
            
            get 
            {
                //TODO: This is a good candidate to be cached
                Dictionary<string, System.Net.Cookie> cookieJar = new Dictionary<string, System.Net.Cookie>();
                var cookie = GetHeaderValues(request, "Cookie").FirstOrDefault() ?? String.Empty;
                string[] cookies = cookie.Split(new char[]{';'}, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < cookies.Length; i++)
                {
                    var parts = cookies[i].Split(new char[]{'='}, StringSplitOptions.RemoveEmptyEntries);
                    if(parts.Length > 1)
                    {
                        cookieJar.Add(parts[0].Trim(), new System.Net.Cookie(parts[0].Trim(), parts[1].Trim()));
                    }

                }

                return cookieJar;
            }
        }

        public IFile[] Files
        {
            //TODO: Can this be implemented?
            get { return new IFile[] { }; }
        }

        public System.Collections.Specialized.NameValueCollection FormData
        {
            get { //TODO: Implement this
                return new System.Collections.Specialized.NameValueCollection();
            }
        }

        public string GetRawBody()
        {
            try
            {
                return new System.Text.UTF8Encoding().GetString(request.Content);
            }
            catch
            {
                return string.Empty;
            }
        }

        public System.Collections.Specialized.NameValueCollection Headers
        {
            get {

                //TODO: If request is constant then this can be cached
                System.Collections.Specialized.NameValueCollection headers = new System.Collections.Specialized.NameValueCollection();
                foreach (var h in request.Headers)
                {
                    foreach (var value in h.Value)
                    {
                        headers.Add(h.Key, value);
                    }
                }

                return headers;
            }
        }


        public string HttpMethod
        {
            get { return request.Method.ToString(); }
        }


        private MemoryStream inputstream = null;
        public System.IO.Stream InputStream
        {
            get 
            {
                if (inputstream == null)
                {
                    inputstream = new MemoryStream(request.Content);
                }

                return inputstream;
            }
        }

        public bool IsLocal
        {
            get { return false; }
        }

        public bool IsSecureConnection
        {
            get { return false; }
        }

        private Dictionary<string, object> items = new Dictionary<string, object>();
        public Dictionary<string, object> Items
        {
            get { return items; }
        }

        public string OperationName
        {
            get;
            set;
        }

        public object OriginalRequest
        {
            get { return this; }
        }

        private string pathInfo;
        public string PathInfo
        {
            get
            {
                if (this.pathInfo == null)
                {
                    var mode = EndpointHost.Config.ServiceStackHandlerFactoryPath;

                    var pos = request.Resource.IndexOf("?");
                    if (pos != -1)
                    {
                        var path = request.Resource.Substring(0, pos);
                        this.pathInfo = global::ServiceStack.WebHost.Endpoints.Extensions.HttpRequestExtensions.GetPathInfo(
                            path,
                            mode,
                            mode ?? "");
                    }
                    else
                    {
                        this.pathInfo = request.Resource;
                    }

                    this.pathInfo = UrlDecode(this.pathInfo);
                    this.pathInfo = NormalizePathInfo(pathInfo, mode);
                }
                return this.pathInfo;
            }
        }

        public System.Collections.Specialized.NameValueCollection QueryString
        {
            get 
            {
                //TODO: This is a good candidate to be cached
                System.Collections.Specialized.NameValueCollection queryString = new System.Collections.Specialized.NameValueCollection();

                var questionMarkIndex = request.Resource.IndexOf('?');

                if (questionMarkIndex > 0)
                {
                    string qs = request.Resource.Substring(questionMarkIndex);
                    var queries = qs.Split('&');
                    foreach (var query in queries)
                    {
                        var kv = query.Split('=');
                        if (kv.Length >= 2)
                        {
                            queryString.Add(kv[0], kv[1]);
                        }
                    }

                }

                return queryString;
            }
        }

        public string RawUrl
        {
            get { return request.Resource; }
        }

        public string RemoteIp
        {
            get { return String.Empty; }
        }

        private string responseContentType;
        public string ResponseContentType
        {
            get { return responseContentType ?? (responseContentType = this.GetResponseContentType()); }
            set { this.responseContentType = value; }
        }

        public bool UseBufferedStream
        {
            get
            {
                return false;
            }
            set
            {
                //do nothing
            }
        }

        public string UserAgent
        {
            get { return GetHeaderValues(request, "User-Agent").FirstOrDefault() ?? String.Empty; }
        }

        public string UserHostAddress
        {
            get { return System.Net.IPAddress.IPv6Loopback.ToString(); }
        }

        public string XForwardedFor
        {
            get { return GetHeaderValues(request, "X-Forwarded-For").FirstOrDefault() ?? String.Empty; }
        }

        public string XRealIp
        {
            get { return GetHeaderValues(request, "X-Real-IP").FirstOrDefault() ?? String.Empty; ; }
        }

        public T TryResolve<T>()
        {
            //TODO: What's this used for?
            throw new NotImplementedException();
        }

        private IEnumerable<string> GetHeaderValues(HttpRequestPacket request, string header)
        {
            header = header.Trim().ToUpperInvariant();
            var hdrs = request.Headers;
            if (hdrs == null) return Enumerable.Empty<String>();

            foreach (var kvp in hdrs)
            {
                if (kvp.Key.ToUpperInvariant() == header)
                {
                    return kvp.Value;
                }
            }

            return Enumerable.Empty<String>();

        }

        private static string NormalizePathInfo(string pathInfo, string handlerPath)
        {
            if (handlerPath != null && pathInfo.TrimStart('/').StartsWith(
                handlerPath, StringComparison.InvariantCultureIgnoreCase))
            {
                return pathInfo.TrimStart('/').Substring(handlerPath.Length);
            }

            return pathInfo;
        }


        //Pulled from ServiceStack.Text/StringExtenstons:
        //TODO: Make note of this at the top of the file.
        private static string UrlDecode(string text)
        {
            if (String.IsNullOrEmpty(text)) return null;

            var bytes = new List<byte>();

            var textLength = text.Length;
            for (var i = 0; i < textLength; i++)
            {
                var c = text[i];
                if (c == '+')
                {
                    bytes.Add(32);
                }
                else if (c == '%')
                {
                    var hexNo = Convert.ToByte(text.Substring(i + 1, 2), 16);
                    bytes.Add(hexNo);
                    i += 2;
                }
                else
                {
                    bytes.Add((byte)c);
                }
            }

            return Encoding.UTF8.GetString(bytes.ToArray());

        }

        public Uri UrlReferrer
        {
            get { return null; }
        }
    }
}
