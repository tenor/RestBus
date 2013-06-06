//Sourced and modified from https://github.com/ServiceStack/ServiceStack/blob/master/src/ServiceStack/ServiceHost/Cookies.cs

using ServiceStack.Common.Web;
using ServiceStack.ServiceHost;
using ServiceStack.WebHost.Endpoints;
using System;
using System.Net;
using System.Text;

namespace RestBus.ServiceStack
{
    public class Cookies : ICookies
    {
        readonly IHttpResponse httpRes;
        private static readonly DateTime Session = DateTime.MinValue;
        private const string RootPath = "/";

        public Cookies(IHttpResponse httpRes)
        {
            this.httpRes = httpRes;
        }

        /// <summary>
        /// Sets a persistent cookie which never expires
        /// </summary>
        public void AddPermanentCookie(string cookieName, string cookieValue, bool? secureOnly = null)
        {
            var cookie = new Cookie(cookieName, cookieValue, RootPath)
            {
                Expires = DateTime.UtcNow.AddYears(20)
            };
            if (secureOnly != null)
            {
                cookie.Secure = secureOnly.Value;
            }
            AddCookie(cookie);
        }

        /// <summary>
        /// Sets a session cookie which expires after the browser session closes
        /// </summary>
        public void AddSessionCookie(string cookieName, string cookieValue, bool? secureOnly = null)
        {
            var cookie = new Cookie(cookieName, cookieValue, RootPath);
            if (secureOnly != null)
            {
                cookie.Secure = secureOnly.Value;
            }
            this.AddCookie(cookie);
        }

        /// <summary>
        /// Deletes a specified cookie by setting its value to empty and expiration to -1 days
        /// </summary>
        public void DeleteCookie(string cookieName)
        {
            var cookie = new Cookie(cookieName, string.Empty, "/")
            {
                Expires = DateTime.UtcNow.AddDays(-1)
            };
            AddCookie(cookie);
        }

        public string GetHeaderValue(Cookie cookie)
        {
            var path = cookie.Expires == Session
                ? "/"
                : cookie.Path ?? "/";

            var sb = new StringBuilder();

            sb.AppendFormat("{0}={1};path={2}", cookie.Name, cookie.Value, path);

            if (cookie.Expires != Session)
            {
                sb.AppendFormat(";expires={0}", cookie.Expires.ToString("R"));
            }

            if (!string.IsNullOrEmpty(cookie.Domain))
            {
                sb.AppendFormat(";domain={0}", cookie.Domain);
            }
            else if (EndpointHost.Config.RestrictAllCookiesToDomain != null)
            {
                sb.AppendFormat(";domain={0}", EndpointHost.Config.RestrictAllCookiesToDomain);
            }

            if (cookie.Secure)
            {
                sb.Append(";Secure");
            }
            if (cookie.HttpOnly)
            {
                sb.Append(";HttpOnly");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Sets a persistent cookie which expires after the given time
        /// </summary>
        public void AddCookie(Cookie cookie)
        {
            var wrapper = this.httpRes as ResponseWrapper;
            if (wrapper != null)
            {
                wrapper.AddHeader(HttpHeaders.SetCookie, GetHeaderValue(cookie));
            }
        }
    }
}
