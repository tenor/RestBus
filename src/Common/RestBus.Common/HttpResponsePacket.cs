using System;
using System.IO;
using System.Net.Http;

namespace RestBus.Common
{
 
    //TODO: Describe why this class exists
    public class HttpResponsePacket : HttpPacket
    {
        public int StatusCode;
        public string StatusDescription;

        public HttpResponsePacket()
        {
        }

        public HttpResponsePacket(HttpResponseMessage response)
        {
            foreach (var hdr in response.Headers)
            {
                AddHttpHeader(hdr);
            }

            if (response.Content != null)
            {
                foreach (var hdr in response.Content.Headers)
                {
                    AddHttpHeader(hdr);
                }
            }

            this.Version = response.Version.ToString();
            this.StatusCode = (int)response.StatusCode;
            this.StatusDescription = response.ReasonPhrase;

            if (response.Content != null)
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    response.Content.CopyToAsync(ms).Wait();
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

                //Write Status line
                WriteText(ms, encoder, "HTTP/" + (String.IsNullOrWhiteSpace(Version) ? "1.1" : Version.Trim()));
                WriteSpace(ms);
                WriteText(ms, encoder, StatusCode.ToString());
                WriteSpace(ms);
                WriteText(ms, encoder, String.IsNullOrWhiteSpace(StatusDescription) ? String.Empty : StatusDescription.Trim());
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

        public static HttpResponsePacket Deserialize(byte[] data)
        {
            if (data == null) throw new ArgumentNullException("data");

            //if (SerializeAsBson)
            //{
            //    return CommonUtils.DeserializeFromBson<HttpResponsePacket>(data);
            //}

            HttpMessageReader reader = new HttpMessageReader(data);

            HttpResponsePacket response = new HttpResponsePacket();

            bool isFirstLine = true;
            string text;
            while ((text = reader.NextLine()) != null)
            {
                if (isFirstLine)
                {
                    isFirstLine = false;
                    string[] components = text.Split(new char[]{' '}, StringSplitOptions.RemoveEmptyEntries);

                    if (components.Length < 2)
                    {
                        throw new InvalidOperationException("Unable to deserialize data into HttpPacket");
                    }

                    if (!components[0].ToUpperInvariant().StartsWith("HTTP/") || components[0].Length <= 5 )
                    {
                        throw new InvalidOperationException("Unable to deserialize data into HttpPacket");
                    }

                    response.Version = components[0].Substring(5).Trim();
                    int statusCode = 0;
                    Int32.TryParse(components[1], out statusCode);
                    response.StatusCode = statusCode;

                    if (components.Length > 2)
                    {
                        string statusDescription = components[2].Trim();

                        for (int i = 3; i < components.Length; i++)
                        {
                            //TODO: Should I convert this to a string builder. Is it worth it?
                            statusDescription += (" " + components[i]);
                        }
                        response.StatusDescription = statusDescription;

                    }
                    else
                    {
                        response.StatusDescription = String.Empty;
                    }
                }
                else
                {
                    ParseLineIntoHeaders(text, response.Headers);
                }
            }

            if (isFirstLine || !reader.IsContentReady)
            {
                throw new InvalidOperationException("Unable to deserialize data into HttpPacket");
            }

            response.Content = reader.GetContent();

            return response;

        }

        public bool TryGetHttpResponseMessage(out HttpResponseMessage response)
        {
            try
            {
                response = new HttpResponseMessage
                {
                    Content = new ByteArrayContent(this.Content ?? new byte[0]),
                    Version = new Version(this.Version),
                    ReasonPhrase = this.StatusDescription,
                    StatusCode = (System.Net.HttpStatusCode)this.StatusCode
                };

                PopulateHeaders(response.Content.Headers, response.Headers);
            }
            catch
            {
                response = null;
                return false;
            }

            return true;
        }


    }
}
