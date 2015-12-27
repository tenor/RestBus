using System;
using System.Net;

namespace RestBus.Client
{
    public class RequestOptions
    {
        protected TimeSpan? _timeout;

        public RequestOptions()
        {
            Headers = new RequestHeaders();
            Cookies = new RequestCookieCollection(Headers);
        }

        public object Tag { get; set; }

        public RequestMessagingProperties Properties { get; set; }

        //TODO: DIsallow illegal times
        public TimeSpan? Timeout
        {
            get { return _timeout; }
            set { _timeout = value; }
        }

        public RequestHeaders Headers { get; set; }

        public RequestCookieCollection Cookies { get; }

        //TODO: Is this code used anywhere ??
        bool TryParseServerCookie(string value, out Cookie cookie)
        {
            const char segmentSeperator = ';';
            const char kvSeperator = '=';

            cookie = null;
            if (String.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            //Split value into different segments
            string[] segments = value.Split(segmentSeperator);

            if (segments.Length == 0) return false;

            string[] kv;
            string segmentKey, segmentValue;

            for (int i = 0; i < segments.Length; i++)
            {
                //Split segment into key and value
                kv = segments[i].Split(new char[]{kvSeperator}, 2);
                
                if (kv.Length < 1) continue;
                segmentKey = kv[0].Trim().ToUpperInvariant();
                if (kv.Length > 1)
                {
                    segmentValue = RemoveQuotes(kv[1]);
                }
                else
                {
                    segmentValue = String.Empty;
                }

                switch (segmentKey)
                {
                    case "DOMAIN":
                        {
                            cookie.Domain = segmentValue;
                        }
                        break;
                    case "PATH":
                        {
                            cookie.Path = segmentValue == String.Empty ? "/" : segmentValue;
                        }
                        break;
                    case "HTTPONLY":
                        {
                            cookie.HttpOnly = true;
                        }
                        break;

                    case "SECURE":
                        {
                            cookie.Secure = true;
                        }
                        break;
                    case "PORT":
                        {
                            cookie.Port = segmentValue;
                        }
                        break;
                    case "EXPIRES":
                        {
                            DateTimeOffset expires;
                            if (DateTimeOffset.TryParse(segmentValue, out expires))
                            {
                                cookie.Expires = expires.DateTime;
                            }
                        }
                        break;
                    /*
                     * 
                     * There is no MaxAge property in System.Net.Cookie
                    case "MAX-AGE":
                        {
                            int maxAge;
                            if (int.TryParse(segmentValue, out maxAge))
                            {
                            }
                        }
                        break;
                     */
                    default:
                        {
                            //Get cookie name and value
                            if (i == 0 && segmentKey != String.Empty)
                            {
                                cookie.Name = kv[0].Trim();
                                cookie.Value = segmentValue;
                            }

                        }
                        break;
                }


            }

            if (!String.IsNullOrEmpty(cookie.Name))
            {
                return true;
            }

            return false;


        }

        string RemoveQuotes(string text)
        {
            if (text != null && text.Length > 1 && text.StartsWith("\"", StringComparison.Ordinal) && text.EndsWith("\"", StringComparison.Ordinal))
            {
                return text.Substring(1, text.Length - 2);
            }

            return text;
        }
        
    }
}
