using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RestBus.Client
{
    public class RequestCookieCollection : IDictionary<string, string>
    {
        RequestHeaders headers;

        public RequestCookieCollection(RequestHeaders requestHeaders)
        {
            if (requestHeaders == null) throw new ArgumentNullException("requestHeaders");
            headers = requestHeaders;
        }

        public void Add(string key, string value)
        {
            if(String.IsNullOrEmpty(key))
            {
                throw new InvalidOperationException("Cookie key cannot be null or empty");
            }
            CheckCharacters(key, true);
            CheckCharacters(value, false);

            var cookies = GetCookies();
            int index = cookies.FindIndex(c => c.Key == key);
            if (index >= 0)
            {
                cookies.RemoveAt(index);
            }

            cookies.Add(new KeyValuePair<string, string>(key, value));
            SetCookies(cookies);
        }

        public bool ContainsKey(string key)
        {
            var cookies = GetCookies();
            return cookies.Any(c => c.Key == key);
        }

        public ICollection<string> Keys
        {
            get {
                var cookies = GetCookies();
                List<string> keys = new List<string>();

                foreach(var cookie in cookies)
                {
                    keys.Add(cookie.Key);
                }

                return keys; 
            }
        }

        public bool Remove(string key)
        {
            var cookies = GetCookies();
            int index = cookies.FindIndex(c => c.Key == key);
            if (index >= 0)
            {
                cookies.RemoveAt(index);
                SetCookies(cookies);
                return true;
            }
            return false;
        }

        public bool TryGetValue(string key, out string value)
        {
            var cookies = GetCookies();
            var found = cookies.Where(c => c.Key == key);

            if (found.Any())
            {
                value = found.First().Value;
                return true;
            }

            value = null;
            return false;
        }

        public ICollection<string> Values
        {
            get {
                var cookies = GetCookies();
                List<string> values = new List<string>();

                foreach (var cookie in cookies)
                {
                    values.Add(cookie.Value);
                }

                return values;
            }
        }

        public string this[string key]
        {
            get
            {
                var cookies = GetCookies();
                int index = cookies.FindIndex(c => c.Key == key);
                if (index >= 0)
                {
                    return cookies[index].Value;
                }
                return null;
            }
            set
            {
                Add(key, value);
            }
        }

        public void Add(KeyValuePair<string, string> item)
        {
            Add(item.Key, item.Value);
        }

        public void Clear()
        {
            var cookies = GetCookies();
            cookies.Clear();
            SetCookies(cookies);
        }

        public bool Contains(KeyValuePair<string, string> item)
        {
            var cookies = GetCookies();
            foreach (var cookie in cookies)
            {
                if (item.Key == cookie.Key && item.Value == cookie.Value)
                {
                    return true;
                }
            }
            return false;
        }

        public void CopyTo(KeyValuePair<string, string>[] array, int arrayIndex)
        {
            var cookies = GetCookies();
            if ((array.Length - arrayIndex) < cookies.Count)
            {
                throw new InvalidOperationException("target array is smaller than cookie collection");
            }

            for (int i = 0; i < cookies.Count; i++)
            {
                array[i + arrayIndex] = cookies[i];
            }
        }

        public int Count
        {
            get
            {
                var cookies = GetCookies();
                return cookies.Count;

            }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool Remove(KeyValuePair<string, string> item)
        {
            var cookies = GetCookies();
            int index = cookies.FindIndex(c => c.Key == item.Key && c.Value == item.Value);
            if (index >= 0)
            {
                cookies.RemoveAt(index);
                SetCookies(cookies);
                return true;
            }
            return false;
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            var cookies = GetCookies();
            return cookies.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            var cookies = GetCookies();
            return cookies.GetEnumerator();
        }

        List<KeyValuePair<string, string>> GetCookies()
        {
            List<KeyValuePair<string, string>> cookies = new List<KeyValuePair<string, string>>();

            IEnumerable<string> existing;
            if (headers.TryGetValues("Cookie", out existing))
            {
                string[] kv;
                foreach (var cookie in existing)
                {
                    if (cookie != null)
                    {
                        kv = cookie.Split(new char[] { '=' }, 2);
                        if (kv.Length > 1)
                        {
                            cookies.Add(new KeyValuePair<string,string>(kv[0], kv[1]));
                        }
                    }
                }
            }

            return cookies;
        }

        void SetCookies(List<KeyValuePair<string, string>> cookies)
        {
            //Remove any existing Cookie header
            IEnumerable<string> existing;
            if (headers.TryGetValues("Cookie", out existing))
            {
                headers.Remove("Cookie");
            }

            //Build cookie string
            StringBuilder sb = new StringBuilder();
            for( int i = 0; i < cookies.Count; i++)
            {
                CheckCharacters(cookies[i].Key, true);
                CheckCharacters(cookies[i].Value, false);

                sb.Append(cookies[i].Key);
                sb.Append('=');
                sb.Append(cookies[i].Value);
                if (i != cookies.Count - 1)
                {
                    sb.Append("; ");
                }
            }

            headers.Add("Cookie", sb.ToString());
        }

        void CheckCharacters(string value, bool checkForEqualsSign)
        {            
            if (value == null) return;

            if (value.IndexOfAny(new char[] { ';', ',', ' ', '\r', '\n', '\t' }) >= 0)
            {
                throw new InvalidOperationException("Invalid characters in cookie string");
            }

            if (checkForEqualsSign)
            {
                if (value.IndexOfAny(new char[] { '=' }) >= 0)
                {
                    throw new InvalidOperationException("Invalid characters in cookie string");
                }
            }
        }
    }
}
