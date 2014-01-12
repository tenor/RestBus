using System;
using System.IO;
using System.Net.Http;

namespace RestBus.Common
{
    //TODO: Describe why this class exists
    public class HttpRequestPacket : HttpPacket
    {
        public string Method;
        public string Resource;

        public HttpRequestPacket()
        {
        }

        public HttpRequestPacket(HttpRequestMessage request)
        {
            foreach (var hdr in request.Headers)
            {
                AddHttpHeader(hdr);
            }

            if (request.Content != null)
            {
                foreach (var hdr in request.Content.Headers)
                {
                    AddHttpHeader(hdr);
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

        public override byte[] Serialize()
        {
            //if (SerializeAsBson)
            //{
            //    return CommonUtils.SerializeAsBson(this);
            //}

            using (MemoryStream ms = new MemoryStream())
            {
                var encoder = new System.Text.UTF8Encoding(false);

                //Write Method Line
                WriteText(ms, encoder, String.IsNullOrWhiteSpace(Method) ? "GET" : Method.Trim().ToUpperInvariant());
                WriteSpace(ms);
                WriteText(ms, encoder, String.IsNullOrWhiteSpace(Resource) ? "/" : Resource.Trim());
                WriteSpace(ms);
                WriteText(ms, encoder, "http/");
                WriteText(ms, encoder, String.IsNullOrWhiteSpace(Version) ? "1.1" : Version.Trim());
                WriteNewLine(ms);

                //Write headers
                WriteHeaders(ms, encoder);

                //Write the new line that seperates headers from content
                WriteNewLine(ms);

                //Write Content
                if (Content != null && Content.Length > 0)
                {
                    ms.Write(Content, 0, Content.Length);
                }

                return ms.ToArray();
            }
        }

        public static HttpRequestPacket Deserialize(byte[] data)
        {
            if (data == null) throw new ArgumentNullException("data");

            //if (SerializeAsBson)
            //{
            //    return Utils.DeserializeFromBson<HttpRequestPacket>(data);
            //}

            HttpMessageReader reader = new HttpMessageReader(data);

            HttpRequestPacket request = new HttpRequestPacket();

            bool isFirstLine = true;
            string text;
            while ((text = reader.NextLine()) != null)
            {
                if (isFirstLine)
                {
                    isFirstLine = false;
                    string[] components = text.Split(new char[]{' '}, StringSplitOptions.RemoveEmptyEntries);

                    if (components.Length < 3)
                    {
                        throw new InvalidOperationException("Unable to deserialize data into HttpPacket");
                    }

                    if (!components[components.Length - 1].ToUpperInvariant().StartsWith("HTTP/") || components[components.Length - 1].Length <= 5 )
                    {
                        throw new InvalidOperationException("Unable to deserialize data into HttpPacket");
                    }

                    request.Version = components[components.Length - 1].Substring(5).Trim();
                    request.Method = components[0].ToUpperInvariant().Trim();

                    string resource = components[1];
                    for (int i = 2; i < components.Length - 1; i++)
                    {
                        //TODO: Should I convert this to a string builder. Is it worth it?
                        resource += (" " + components[i]);
                    }

                    request.Resource = resource;
                }
                else
                {
                    ParseLineIntoHeaders(text, request.Headers);
                }
            }

            if (isFirstLine || !reader.IsContentReady)
            {
                throw new InvalidOperationException("Unable to deserialize data into HttpPacket");
            }

            request.Content = reader.GetContent();

            return request;

        }

        public bool TryGetHttpRequestMessage(string virtualPath, out HttpRequestMessage request)
        {
            try
            {
                request = new HttpRequestMessage
                {
                    Content = new ByteArrayContent(this.Content ?? new byte[0]),
                    Version = new Version(this.Version),
                    Method = new HttpMethod(this.Method ?? "GET"),
                    RequestUri = GetUriFromResource(virtualPath, this.Resource)
                };

                PopulateHeaders(request.Content.Headers, request.Headers);
            }
            catch
            {
                request = null;
                return false;
            }

            return true;
        }

        //This method tries to get an absolute uri from the provided resource
        private static string machineHostName;
        private static Uri GetUriFromResource(string virtualPath, string resource)
        {

            if (String.IsNullOrEmpty(virtualPath))
            {
                virtualPath = "/";
            }

            if (!virtualPath.EndsWith("/"))
            {
                virtualPath += "/";
            }

            if (resource != null && resource.StartsWith("/"))
            {
                resource = resource.Substring(1);
            }

            string path, query;
            int qmarkIndex = resource.IndexOf('?');
            if (qmarkIndex == -1)
            {
                path = virtualPath + resource;
                query = string.Empty;
            }
            else
            {
                path = virtualPath + resource.Substring(0, qmarkIndex);
                query = resource.Substring(qmarkIndex);
            }
            bool success = false;

            Uri result = null;
            try
            {
                if(machineHostName == null)
                {
                    machineHostName = Environment.MachineName ?? "localhost";
                }
                result = new UriBuilder("http", machineHostName, 80, path, query).Uri;
                success = true;
            }
            catch { }

            if (success) return result;

            if (machineHostName != "localhost")
            {
                //Something may be wrong with machine name, and so try localhost
                try
                {
                    result = new UriBuilder("http", "localhost", 80, path, query).Uri;
                    success = true;
                }
                catch { }

                if (success)
                {
                    machineHostName = "localhost";
                    return result;
                }
            }

            //Return a Relative Uri
            return new Uri(resource, UriKind.RelativeOrAbsolute);
        }



    }
}
