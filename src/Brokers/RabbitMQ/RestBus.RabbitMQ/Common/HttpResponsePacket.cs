using RestBus.RabbitMQ.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;

namespace RestBus.RabbitMQ
{
 
    //TODO: Describe why this class exists
    public class HttpResponsePacket : HttpPacket
    {
        public int StatusCode;
        public string StatusDescription;


        static string[] contentHeaders = { "ALLOW", "CONTENT-DISPOSITION", "CONTENT-ENCODING", "CONTENT-LANGUAGE", "CONTENT-LOCATION", "CONTENT-MD5", 
                                             "CONTENT-RANGE", "CONTENT-TYPE", "EXPIRES", "LAST-MODIFIED", "CONTENT-LENGTH"  };


        public HttpResponsePacket()
        {
        }

        public override byte[] Serialize()
        {

            if (SerializeAsBson)
            {
                return Utils.SerializeAsBson(this);
            }

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

                string hdrKey;
                foreach (var hdr in this.Headers)
                {
                    if (hdr.Key == null) continue;

                    hdrKey = hdr.Key.Trim().ToUpperInvariant();

                    if (hdrKey == "CONTENT-LENGTH") continue; //Content Length is automaitically calculated

                    if (Array.IndexOf<String>(contentHeaders, hdrKey) >= 0)
                    {
                        //TODO: Confirm if HttpResponseMessage will break headers into "," commas whereas in actuality header in Packet is an entire header
                        response.Content.Headers.Add(hdr.Key.Trim(), hdr.Value);
                    }
                    else
                    {
                        response.Headers.Add(hdr.Key.Trim(), hdr.Value);
                    }

                    //TODO: Check if a string can be parsed properly into the typed header

                    //Test adding multiple headers of the same name will do. // Look up the Add overload that takes an ienumerable<string> to figure out its purpose.
                }
            }
            catch
            {
                response = null;
                return false;
            }

            return true;
        }


        public static HttpResponsePacket Deserialize(byte[] data)
        {
            if (data == null) throw new ArgumentNullException("data");

            if (SerializeAsBson)
            {
                return Utils.DeserializeFromBson<HttpResponsePacket>(data);
            }

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



    }
}
