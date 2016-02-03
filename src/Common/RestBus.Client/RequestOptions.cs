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

        public string ServiceName { get; set; }

        //TODO: DIsallow illegal times: allow TimeSpan.Zero, allow TimeSpam.Infinite but certainly not negative timespans.
        public TimeSpan? Timeout
        {
            get { return _timeout; }
            set { _timeout = value; }
        }

        public bool? ExpectsReply { get; set; }

        public RequestHeaders Headers { get; set; }

        public RequestCookieCollection Cookies { get; }        
    }
}
