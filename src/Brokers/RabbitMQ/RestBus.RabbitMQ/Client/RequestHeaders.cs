using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RestBus.RabbitMQ.Client
{
    public class RequestHeaders : IEnumerable<KeyValuePair<string, IEnumerable<string>>>, System.Collections.IEnumerable
    {
        Dictionary<string, IEnumerable<string>> headers = new Dictionary<string, IEnumerable<string>>();

        public void Add(string name, string value)
        {
            //TODO: Check for invalid HTTP Header Name and Value (from RFC specs)

            if (headers.ContainsKey(name))
            {
                List<string> val = (List<string>) headers[name];
                val.Add(value);
            }
            else
            {
                headers.Add(name, new List<string> { value });
            }
        }

        public void Add(string name, IEnumerable<string> value)
        {
            foreach (var val in value)
            {
                Add(name, val);
            }
        }

        public bool Contains(string name)
        {
            return headers.ContainsKey(name);
        }

        public ICollection<string> Names
        {
            get { return headers.Keys; }
        }

        public bool Remove(string name)
        {
            return headers.Remove(name);
        }

        public void Clear()
        {
            headers.Clear();
        }

        public int Count
        {
            get { return headers.Count; }
        }

        public bool TryGetValues(string name, out IEnumerable<string> value)
        {
            return headers.TryGetValue(name, out value);
        }


        IEnumerable<string> this[string key]
        {
            get
            {
                return headers[key];
            }
            set
            {
                List<string> list = new List<string>(value);
                headers[key] = list;
            }
        }


        public IEnumerable<string> Values(string name)
        {
            IEnumerable<string> values;
            if (!TryGetValues(name, out values))
            {
                throw new InvalidOperationException("HTTP Header not found");
            }

            return values;
        }


        public IEnumerator<KeyValuePair<string, IEnumerable<string>>> GetEnumerator()
        {
            return headers.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
