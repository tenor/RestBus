using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Hosting;
using System.Web.Http.Routing;

namespace RestBus.WebApi
{
    public static class HttpRequestHelperExtensions
    {

        const string RequestContextKey = "MS_RequestContext";

        /// <summary>Gets the <see cref="HttpRequestContext"/> associated with this request.</summary>
        /// <param name="request">The HTTP request.</param>
        /// <returns>The <see cref="HttpRequestContext"/> associated with this request.</returns>
        internal static HttpRequestContext GetRequestContext(this HttpRequestMessage request)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            return request.GetProperty<HttpRequestContext>(RequestContextKey);
        }

        /// <summary>Gets an <see cref="HttpRequestContext"/> associated with this request.</summary>
        /// <param name="request">The HTTP request.</param>
        /// <param name="context">The <see cref="HttpRequestContext"/> to associate with this request.</param>
        internal static void SetRequestContext(this HttpRequestMessage request, HttpRequestContext context)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            request.Properties[RequestContextKey] = context;
        }

        internal static void SetSynchronizationContext(this HttpRequestMessage request, SynchronizationContext synchronizationContext)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            request.Properties[HttpPropertyKeys.SynchronizationContextKey] = synchronizationContext;
        }

        /// <summary>
        /// Sets the <see cref="HttpConfiguration"/> for the given request.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <param name="configuration">The <see cref="HttpConfiguration"/> to set.</param>
        internal static void SetConfiguration(this HttpRequestMessage request, HttpConfiguration configuration)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            if (configuration == null)
            {
                throw new ArgumentNullException("configuration");
            }

            HttpRequestContext requestContext = GetRequestContext(request);

            if (requestContext != null)
            {
                requestContext.Configuration = configuration;
            }

            request.Properties[HttpPropertyKeys.HttpConfigurationKey] = configuration;
        }

        internal static X509Certificate2 LegacyGetClientCertificate(this HttpRequestMessage request)
        {
            X509Certificate2 result = null;

            if (!request.Properties.TryGetValue(HttpPropertyKeys.ClientCertificateKey, out result))
            {
                // now let us get out the delegate and try to invoke it
                Func<HttpRequestMessage, X509Certificate2> retrieveCertificate;

                if (request.Properties.TryGetValue(HttpPropertyKeys.RetrieveClientCertificateDelegateKey, out retrieveCertificate))
                {
                    result = retrieveCertificate(request);

                    if (result != null)
                    {
                        request.Properties.Add(HttpPropertyKeys.ClientCertificateKey, result);
                    }
                }
            }

            return result;
        }

        internal static bool LegacyShouldIncludeErrorDetail(this HttpRequestMessage request)
        {
            HttpConfiguration configuration = request.GetConfiguration();
            IncludeErrorDetailPolicy includeErrorDetailPolicy = IncludeErrorDetailPolicy.Default;
            if (configuration != null)
            {
                includeErrorDetailPolicy = configuration.IncludeErrorDetailPolicy;
            }
            switch (includeErrorDetailPolicy)
            {
                case IncludeErrorDetailPolicy.Default:
                    Lazy<bool> includeErrorDetail = request.GetProperty<Lazy<bool>>(HttpPropertyKeys.IncludeErrorDetailKey);
                    if (includeErrorDetail != null)
                    {
                        // If we are on webhost and the user hasn't changed the IncludeErrorDetailPolicy
                        // look up into the Request's property bag else default to LocalOnly.
                        return includeErrorDetail.Value;
                    }

                    goto case IncludeErrorDetailPolicy.LocalOnly;

                case IncludeErrorDetailPolicy.LocalOnly:
                    return request.IsLocal();

                case IncludeErrorDetailPolicy.Always:
                    return true;

                case IncludeErrorDetailPolicy.Never:
                default:
                    return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the request originates from a local address or not.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <returns><see langword="true"/> if the request originates from a local address; otherwise, <see langword="false"/>.</returns>
        internal static bool IsLocal(this HttpRequestMessage request)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            HttpRequestContext requestContext = GetRequestContext(request);

            if (requestContext != null)
            {
                return requestContext.IsLocal;
            }

            return request.LegacyIsLocal();
        }

        internal static bool LegacyIsLocal(this HttpRequestMessage request)
        {
            Lazy<bool> isLocal = request.GetProperty<Lazy<bool>>(HttpPropertyKeys.IsLocalKey);

            return isLocal == null ? false : isLocal.Value;
        }

        internal static IHttpRouteData LegacyGetRouteData(this HttpRequestMessage request)
        {
            return request.GetProperty<IHttpRouteData>(HttpPropertyKeys.HttpRouteDataKey);
        }

        internal static HttpConfiguration LegacyGetConfiguration(this HttpRequestMessage request)
        {
            return request.GetProperty<HttpConfiguration>(HttpPropertyKeys.HttpConfigurationKey);
        }

        /// <summary>
        /// Gets the value of <typeparamref name="T"/> associated with the specified key or <c>default</c> value if
        /// either the key is not present or the value is not of type <typeparamref name="T"/>. 
        /// </summary>
        /// <typeparam name="T">The type of the value associated with the specified key.</typeparam>
        /// <param name="collection">The <see cref="IDictionary{TKey,TValue}"/> instance where <c>TValue</c> is <c>object</c>.</param>
        /// <param name="key">The key whose value to get.</param>
        /// <param name="value">When this method returns, the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter.</param>
        /// <returns><c>true</c> if key was found, value is non-null, and value is of type <typeparamref name="T"/>; otherwise false.</returns>
        internal static bool TryGetValue<T>(this IDictionary<string, object> collection, string key, out T value)
        {

            object valueObj;
            if (collection.TryGetValue(key, out valueObj))
            {
                if (valueObj is T)
                {
                    value = (T)valueObj;
                    return true;
                }
            }

            value = default(T);
            return false;
        }

        private static T GetProperty<T>(this HttpRequestMessage request, string key)
        {
            T value;
            request.Properties.TryGetValue(key, out value);
            return value;
        }
    }
}
