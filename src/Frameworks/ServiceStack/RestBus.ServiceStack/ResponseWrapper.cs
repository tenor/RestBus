using RestBus.RabbitMQ.Subscriber;
using ServiceStack.ServiceHost;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RestBus.ServiceStack
{
    public class ResponseWrapper : IHttpResponse
    {
        //TODO: Fix this class -- See ServiceStacks HttpListenerResponseWrapper class

        System.Collections.Specialized.NameValueCollection headers = new System.Collections.Specialized.NameValueCollection();

        public System.Collections.Specialized.NameValueCollection Headers
        {
            get { return headers; }
        } 

        public void AddHeader(string name, string value)
        {
            //TODO: Make sure header name is valid

            headers.Add(name, value);
        }


        public void Close()
        {
            //TODO: Close outputstream
            //Also close request inputstream
            if (!_isClosed)
            {
                _isClosed = true;
                outputStream.Close();
            }


        }

        public string ContentType
        {
            get
            {
                return headers["Content-Type"];
            }
            set
            {
                headers["Content-Type"] = value;
            }
        }

        Cookies cookies;
        public ICookies Cookies
        {
            get { 
                if (cookies == null) { cookies = new Cookies(this); }
                return cookies;
            }
        }

        public void End()
        {
            Close();
        }

        public void Flush()
        {
            outputStream.Flush();
        }

        private bool _isClosed = false;
        public bool IsClosed
        {
            //TODO: Look at the proper way to implement this -- does this refer to the strem's closed state
            get { return _isClosed; }
        }

        public object OriginalResponse
        {
            get { return this; }
        }

        private MemoryStream outputStream = null;
        public System.IO.Stream OutputStream
        {
            get
            {
                if (outputStream == null)
                {
                    outputStream = new MemoryStream();
                }

                return outputStream;
            }
        }

        public void Redirect(string url)
        {
            //TODO: Write code that sets the redirection URL and status and/or Figure out what HttpListenerResponse.Redirecte
            throw new NotImplementedException();
        }

        private int _statuscode = (int)System.Net.HttpStatusCode.OK;
        public int StatusCode
        {
            get
            {
                return _statuscode;

            }
            set
            {
                _statuscode = value;
            }
        }

        private string _statusDescription;
        public string StatusDescription
        {
            get
            {
                if (_statusDescription == null)
                {
                    _statusDescription = GetStatusDescription(_statuscode) ?? String.Empty;
                }

                return _statusDescription;
            }
            set
            {
                _statusDescription = value;
            }
        }

        public void Write(string text)
        {
            try
            {
                var output = System.Text.Encoding.UTF8.GetBytes(text);
                outputStream.Write(output, 0, output.Length);
                Close();
            }
            catch (Exception ex)
            {
                //TODO : Log this
                throw;
            }
        }

        private string GetStatusDescription(int statusCode)
        {
            switch (statusCode)
            {
                #region 1xx Informational codes
                case 100:
                    return "Continue";
                case 101:
                    return "Switching Protocols";
                #endregion

                #region 2xx Successful codes
                case 200:
                    return "OK";
                case 201:
                    return "Created";
                case 202:
                    return "Accepted";
                case 203:
                    return "Non-Authoritative Information";
                case 204:
                    return "No Content";
                case 205:
                    return "Reset Content";
                case 206:
                    return "Partial Content";
                #endregion

                #region 3xx Redirection codes
                case 300:
                    return "Multiple Choices";
                case 301:
                    return "Moved Permanently";
                case 302:
                    return "Found";
                case 303:
                    return "See Other";
                case 304:
                    return "Not Modified";
                case 305:
                    return "Use Proxy";
                case 307:
                    return "Temporary Redirect";
                #endregion

                #region 4xx Client Error codes
                case 400:
                    return "Bad Request";
                case 401:
                    return "Unauthorized";
                case 402:
                    return "Payment Required";
                case 403:
                    return "Forbidden";
                case 404:
                    return "Not Found";
                case 405:
                    return "Method Not Allowed";
                case 406:
                    return "Not Acceptable";
                case 407:
                    return "Proxy Authentication Required";
                case 408:
                    return "Request Timeout";
                case 409:
                    return "Conflict";
                case 410:
                    return "Gone";
                case 411:
                    return "Length Required";
                case 412:
                    return "Precondition Failed";
                case 413:
                    return "Request Entity Too Large";
                case 414:
                    return "Request-URI Too Long";
                case 415:
                    return "Unsupported Media Type";
                case 416:
                    return "Requested Range Not Satisfiable";
                case 417:
                    return "Expectation Failed";

                #endregion

                #region 5xx Server Error codes
                case 500:
                    return "Internal Server Error";
                case 501:
                    return "Not Implemented";
                case 502:
                    return "Bad Gateway";
                case 503:
                    return "Service Unavailable";
                case 504:
                    return "Gateway Timeout";
                case 505:
                    return "HTTP Version Not Supported";

                #endregion

                default:
                    return null;
            }

        }
    }
}
