using Microsoft.AspNet.Http.Features;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections;
using Microsoft.Extensions.Primitives;
using System.IO;
using RestBus.Common;
using Microsoft.AspNet.Http;

namespace RestBus.AspNet
{
    internal class ServiceMessage : IFeatureCollection,
                                 IHttpRequestFeature,
                                 IHttpResponseFeature
    {
        int featureRevision;
        object _currentIHttpRequestFeature;
        object _currentIHttpResponseFeature;
        List<KeyValuePair<Type, object>> otherFeatures;
        InterlockedBoolean _disposed;
        string _scheme;
        string _pathBase;
        string _path;
        string _queryString;
        InterlockedBoolean _responseHasStarted;
        readonly object _onStartingSync = new object();
        readonly object _onCompletedSync = new object();
        List<KeyValuePair<Func<object, Task>, object>> _onStarting;
        List<KeyValuePair<Func<object, Task>, object>> _onCompleted;

        internal bool ResponseHasStarted
        {
            get
            {
                return _responseHasStarted.IsTrue;
            }
            set
            {
                _responseHasStarted.Set(value);
            }
        }

        public void OnStarting(Func<object, Task> callback, object state)
        {
            lock (_onStartingSync)
            {
                if (_onStarting == null)
                {
                    _onStarting = new List<KeyValuePair<Func<object, Task>, object>>();
                }
                _onStarting.Add(new KeyValuePair<Func<object, Task>, object>(callback, state));
            }
        }

        public void OnCompleted(Func<object, Task> callback, object state)
        {
            lock (_onCompletedSync)
            {
                if (_onCompleted == null)
                {
                    _onCompleted = new List<KeyValuePair<Func<object, Task>, object>>();
                }
                _onCompleted.Add(new KeyValuePair<Func<object, Task>, object>(callback, state));
            }
        }

        internal ServiceMessage()
        {
            _currentIHttpRequestFeature = this;
            _currentIHttpResponseFeature = this;
        }

        #region IFeatureCollection Implementation

        object IFeatureCollection.this[Type key]
        {
            get
            {
                if (key == typeof(IHttpRequestFeature)) { return _currentIHttpRequestFeature; }
                if (key == typeof(IHttpResponseFeature)) { return _currentIHttpResponseFeature; }

                if (otherFeatures == null) return null;
                foreach (var kv in otherFeatures)
                {
                    if (kv.Key == key) return kv.Value;
                }

                return null;
            }

            set
            {
                featureRevision++;
                if (key == typeof(IHttpRequestFeature)) { _currentIHttpRequestFeature = value; }
                if (key == typeof(IHttpResponseFeature)) { _currentIHttpResponseFeature = value; }

                if (otherFeatures == null)
                {
                    otherFeatures = new List<KeyValuePair<Type, object>>();
                }

                for (int i = 0; i < otherFeatures.Count; i++)
                {
                    if (otherFeatures[i].Key == key)
                    {
                        otherFeatures[i] = new KeyValuePair<Type, object>(key, value);
                        return;
                    }
                }

                otherFeatures.Add(new KeyValuePair<Type, object>(key, value));
            }
        }

        bool IFeatureCollection.IsReadOnly
        {
            get
            {
                return false;
            }
        }

        int IFeatureCollection.Revision
        {
            get
            {
                return featureRevision;
            }
        }

        public void Dispose()
        {
            if (_disposed.IsTrue) return;
            _disposed.Set(true);

            if (((IHttpRequestFeature)this).Body != null)
            {
                try
                {
                    ((IHttpRequestFeature)this).Body.Dispose();
                }
                catch { }
            }

            if (((IHttpResponseFeature)this).Body != null)
            {
                try
                {
                    ((IHttpResponseFeature)this).Body.Dispose();
                }
                catch { }
            }

            IDisposable disposable;
            foreach (var feature in otherFeatures)
            {
                if (feature.Value != null)
                {
                    disposable = feature.Value as IDisposable;
                    if(disposable != null)
                    {
                        try
                        {
                            disposable.Dispose();
                        }
                        catch
                        {
                        }
                    }
                }
            }
        }

        IEnumerator<KeyValuePair<Type, object>> IEnumerable<KeyValuePair<Type, object>>.GetEnumerator()
        {
            return GetFeatureCollectionEnumerable().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetFeatureCollectionEnumerable().GetEnumerator();
        }


        IEnumerable<KeyValuePair<Type, object>> GetFeatureCollectionEnumerable()
        {
            if (_currentIHttpRequestFeature != null)
            {
                yield return new KeyValuePair<Type, object>(typeof(IHttpRequestFeature), _currentIHttpRequestFeature as IHttpRequestFeature);
            }
            if (_currentIHttpResponseFeature != null)
            {
                yield return new KeyValuePair<Type, object>(typeof(IHttpResponseFeature), _currentIHttpResponseFeature as IHttpResponseFeature);
            }

            if (otherFeatures != null)
            {
                foreach (var feature in otherFeatures)
                {
                    yield return feature;
                }
            }
        }

        #endregion

        #region IHttpRequestFeature Implementation
        string IHttpRequestFeature.Protocol
        {
            get; set;
        }

        string IHttpRequestFeature.Scheme
        {
            get
            {
                return _scheme ?? "http";
            }

            set
            {
                _scheme = value;
            }
        }

        string IHttpRequestFeature.Method
        {
            get; set;
        }

        string IHttpRequestFeature.PathBase
        {
            get
            {
                return _pathBase ?? String.Empty;
            }

            set
            {
                _pathBase = value;
            }
        }

        string IHttpRequestFeature.Path
        {
            get { return _path ?? String.Empty; }
            set { _path = value; }
        }

        string IHttpRequestFeature.QueryString
        {
            get { return _queryString ?? String.Empty; }
            set { _queryString = value; }
        }

        IHeaderDictionary IHttpRequestFeature.Headers
        {
            get; set;
        }

        Stream IHttpRequestFeature.Body
        {
            get; set;
        }

        #endregion

        #region IHttpResponseFeature Implementation
        int IHttpResponseFeature.StatusCode
        {
            get; set;
        }

        string IHttpResponseFeature.ReasonPhrase
        {
            get; set;
        }

        IHeaderDictionary IHttpResponseFeature.Headers
        {
            get; set;
        }

        Stream IHttpResponseFeature.Body
        {
            get; set;
        }

        bool IHttpResponseFeature.HasStarted
        {
            get
            {
                return ResponseHasStarted;
            }
        }

        void IHttpResponseFeature.OnStarting(Func<object, Task> callback, object state)
        {
            OnStarting(callback, state);
        }

        void IHttpResponseFeature.OnCompleted(Func<object, Task> callback, object state)
        {
            OnCompleted(callback, state);
        }

        #endregion
    }
}
