using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RestBus.RabbitMQ.Common
{
    internal class HttpMessageReader
    {
        int lineStart = 0;
        int lineEnd = -1;
        readonly int len;
        bool contentMode = false;
        bool readLines = false;
        byte[] data;
        System.Text.UTF8Encoding encoder = new System.Text.UTF8Encoding(false);
        public HttpMessageReader(byte[] data)
        {
            if (data == null) throw new ArgumentNullException("data");
            this.data = data;
            len = data.Length;

        }

        public string NextLine()
        {
            if (contentMode || lineStart >= len ) return null;

            while (++lineEnd < len )
            {
                if (data[lineEnd] == (byte)'\n')
                {
                    if (lineEnd == lineStart && readLines)
                    {
                        contentMode = true;
                        lineStart = lineEnd + 1;
                        return null;

                    }
                    else
                    {
                        string text = encoder.GetString(data, lineStart, (lineEnd - lineStart));
                        lineStart = lineEnd + 1;
                        readLines = true;
                        return text;
                    }
                }
            }

            return null;
                
        }

        public bool IsContentReady
        {
            get
            {
                return contentMode;
            }
        }

        public byte[] GetContent()
        {
            if (!contentMode) throw new InvalidOperationException("Content has not been read yet");
            if (lineStart >= len) return new byte[0];

            byte[] content = new byte[len - lineStart];
            Array.Copy(data, lineStart, content, 0, len - lineStart);

            return content;

        }
    }
}
