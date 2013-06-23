using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Net.Http.Headers;

namespace RestBus.RabbitMQ
{
    //TODO: Describe the purpose of this class
    public abstract class HttpPacket
    {
        protected const bool SerializeAsBson = false;

        public byte[] Content;
        public readonly Dictionary<string, IEnumerable<string>> Headers = new Dictionary<string, IEnumerable<string>>();
        public string Version;

        static string[] contentOnlyHeaders = { "ALLOW", "CONTENT-DISPOSITION", "CONTENT-ENCODING", "CONTENT-LANGUAGE", "CONTENT-LOCATION", "CONTENT-MD5", 
                                             "CONTENT-RANGE", "CONTENT-TYPE", "EXPIRES", "LAST-MODIFIED", "CONTENT-LENGTH"  };

        public abstract byte[] Serialize();

        #region Helper Methods to Populate to and from HttpRequestMessage / HttpResponseMessage headers
        protected void PopulateHeaders(HttpContentHeaders contentHeaders, HttpHeaders generalHeaders)
        {
            string hdrKey;
            foreach (var hdr in this.Headers)
            {
                if (hdr.Key == null) continue;

                hdrKey = hdr.Key.Trim().ToUpperInvariant();

                if (hdrKey == "CONTENT-LENGTH") continue; //Content Length is automaitically calculated

                if (Array.IndexOf<String>(contentOnlyHeaders, hdrKey) >= 0)
                {
                    //TODO: Confirm if HttpResponseMessage/HttpRequestMessage will break headers into "," commas whereas in actuality header in Packet is an entire header
                    contentHeaders.Add(hdr.Key.Trim(), hdr.Value);
                }
                else
                {
                    generalHeaders.Add(hdr.Key.Trim(), hdr.Value);
                }

                //TODO: Check if a string can be parsed properly into the typed header

                //Test adding multiple headers of the same name will do. // Look up the Add overload that takes an ienumerable<string> to figure out its purpose.
            }
        }

        protected void AddHttpHeader(KeyValuePair<string, IEnumerable<string>> hdr)
        {
            if (this.Headers.ContainsKey(hdr.Key))
            {
                ((List<string>)this.Headers[hdr.Key]).Add(String.Join(", ", hdr.Value.ToArray()));
            }
            else
            {
                this.Headers.Add(hdr.Key, new List<string>() { String.Join(", ", hdr.Value.ToArray()) });
            }
        }
        #endregion

        #region Serialization/Derserialization helper methods
        protected static void ParseLineIntoHeaders(string text, Dictionary<string, IEnumerable<string>> headers)
        {
            int colonPos = text.IndexOf(':');
            if (colonPos == -1)
            {
                throw new InvalidOperationException("Unable to deserialize data into HttpPacket");
            }

            string name = text.Substring(0, colonPos).Trim();
            string value = text.Substring(colonPos + 1).TrimStart();

            if (headers.ContainsKey(name))
            {
                ((List<string>)headers[name]).Add(value);
            }
            else
            {
                headers[name] = new List<string> { value };
            }
        }

        protected void WriteHeaders(MemoryStream ms, System.Text.UTF8Encoding encoder)
        {
            //TODO: Confirm that the purpose of an IEnumerable<string> value is to store multiple headers of the same name

            //Write Headers
            string name;
            foreach (var header in Headers)
            {
                name = (header.Key ?? String.Empty).Trim().Replace(":", String.Empty);
                if (name.Length > 0 && name.ToUpperInvariant() != "CONTENT-LENGTH") //content-length isn't written while serializing
                {
                    if (header.Value == null)
                    {
                        WriteSingleHeader(ms, encoder, name, String.Empty);
                    }
                    else
                    {
                        foreach (var value in header.Value)
                        {
                            WriteSingleHeader(ms, encoder, name, value);
                        }
                    }
                }
            }
        }

        protected static void WriteSingleHeader(MemoryStream ms, System.Text.UTF8Encoding encoder, string name, string value)
        {
            string sanitizedValue = (value ?? String.Empty).Replace("/n", String.Empty);
            WriteText(ms, encoder, name);
            ms.WriteByte((byte)':');
            WriteSpace(ms);
            WriteText(ms, encoder, sanitizedValue);
            WriteNewLine(ms);
        }

        protected static void WriteSpace(MemoryStream ms)
        {
            ms.WriteByte((byte)' ');
        }

        protected static void WriteNewLine(MemoryStream ms)
        {
            ms.WriteByte((byte)'\n');
        }

        protected static void WriteText(MemoryStream ms, System.Text.Encoding encoder, string text)
        {
            byte[] buffer = encoder.GetBytes(text);
            ms.Write(buffer, 0, buffer.Length);
        }

        #endregion
    }
}
