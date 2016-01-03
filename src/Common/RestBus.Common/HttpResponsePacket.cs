using System;
using System.IO;

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

                    if (!components[0].StartsWith("HTTP/", StringComparison.OrdinalIgnoreCase) || components[0].Length <= 5 )
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
                            //TODO: Convert to a string builder? Is it worth it?
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
