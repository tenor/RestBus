using System;
using System.IO;

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
                WriteText(ms, encoder, "HTTP/");
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
            //    return CommonUtils.DeserializeFromBson<HttpRequestPacket>(data);
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

                    if (!components[components.Length - 1].StartsWith("HTTP/", StringComparison.OrdinalIgnoreCase) || components[components.Length - 1].Length <= 5 )
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

        /// <summary>
        /// Builds a <see cref="System.Uri"/> for this <see cref="HttpRequestPacket"/>'s resource
        /// </summary>
        /// <param name="virtualPath">The virtual path (optional)</param>
        /// <param name="hostname">The optional hostname (optional)</param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"> Throws InvalidOperationException if the Resource field is null.</exception>
        public Uri BuildUri(string virtualPath, string hostname)
        {
            if (this.Resource == null) throw new InvalidOperationException("Resource field is null.");
            return BuildUri(this.Resource, virtualPath, hostname);
        }

        /// <summary>
        /// Builds a <see cref="System.Uri"/> for a specified resource, virtualPath and hostname
        /// </summary>
        /// <param name="resource"></param>
        /// <param name="virtualPath"></param>
        /// <param name="hostname"></param>
        /// <returns></returns>
        private static Uri BuildUri(string resource, string virtualPath, string hostname)
        {

            if (String.IsNullOrEmpty(virtualPath))
            {
                virtualPath = "/";
            }

            if (!virtualPath.EndsWith("/"))
            {
                virtualPath += "/";
            }

            var originalResource = resource;

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
            if (hostname == null) hostname = "localhost";
            try
            {                
                result = new UriBuilder("http", hostname, 80, path, query).Uri;
                success = true;
            }
            catch { }

            if (success) return result;

            if (hostname != "localhost")
            {
                //Something may be wrong with host name, and so try localhost
                try
                {
                    result = new UriBuilder("http", "localhost", 80, path, query).Uri;
                    success = true;
                }
                catch { }

                if (success) return result;

            }

            //Return a Relative Uri
            return new Uri(originalResource, UriKind.RelativeOrAbsolute);
        }



    }
}
