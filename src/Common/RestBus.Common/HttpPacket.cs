using System;
using System.Collections.Generic;
using System.IO;

namespace RestBus.Common
{
    //TODO: Describe the purpose of this class
    public abstract class HttpPacket
    {
        protected const bool SerializeAsBson = false;

        public byte[] Content;
        public readonly Dictionary<string, IEnumerable<string>> Headers = new Dictionary<string, IEnumerable<string>>();
        public string Version;

        public abstract byte[] Serialize();


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
