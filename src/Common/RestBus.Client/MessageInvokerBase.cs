using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace RestBus.Client
{
    //TODO: Rename project.json.disabled to project.json once https://github.com/JamesNK/Newtonsoft.Json/issues/763 is resolved

    /// <summary>
    /// Base class for RestBus client implementations.
    /// </summary>
    public abstract class MessageInvokerBase : HttpMessageInvoker
    {
        const string REQUEST_OPTIONS_KEY = "_RestBus_request_options";

        public MessageInvokerBase(HttpMessageHandler handler, bool disposeHandler) : base(handler, disposeHandler)
        {
        }

        #region xxxAsync Methods attached to the HTTP Client

        //All the xxxAsync methods should have been implemented as extension methods (all except SendAsync), oh well....

        /// <summary>Send a DELETE request to the specified Uri as an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public Task<HttpResponseMessage> DeleteAsync(string requestUri)
        {
            return DeleteAsync(GetUri(requestUri));
        }

        /// <summary>Send a DELETE request to the specified Uri as an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public Task<HttpResponseMessage> DeleteAsync(Uri requestUri)
        {
            return DeleteAsync(requestUri, CancellationToken.None);
        }

        /// <summary>Send a DELETE request to the specified Uri with a cancellation token as an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public Task<HttpResponseMessage> DeleteAsync(string requestUri, CancellationToken cancellationToken)
        {
            return DeleteAsync(GetUri(requestUri), cancellationToken);
        }

        /// <summary>Send a DELETE request to the specified Uri with a cancellation token as an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public Task<HttpResponseMessage> DeleteAsync(Uri requestUri, CancellationToken cancellationToken)
        {
            return SendAsync(new HttpRequestMessage(HttpMethod.Delete, requestUri), cancellationToken);
        }

        /// <summary>Send a GET request to the specified Uri as an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public Task<HttpResponseMessage> GetAsync(string requestUri)
        {
            return GetAsync(GetUri(requestUri));
        }

        /// <summary>Send a GET request to the specified Uri as an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public Task<HttpResponseMessage> GetAsync(Uri requestUri)
        {
            return GetAsync(requestUri, HttpCompletionOption.ResponseContentRead);
        }

        /// <summary>Send a GET request to the specified Uri with an HTTP completion option as an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.</returns>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <param name="completionOption">An HTTP completion option value that indicates when the operation should be considered completed.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public Task<HttpResponseMessage> GetAsync(string requestUri, HttpCompletionOption completionOption)
        {
            return GetAsync(GetUri(requestUri), completionOption);
        }

        /// <summary>Send a GET request to the specified Uri with an HTTP completion option as an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <param name="completionOption">An HTTP  completion option value that indicates when the operation should be considered completed.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public Task<HttpResponseMessage> GetAsync(Uri requestUri, HttpCompletionOption completionOption)
        {
            return GetAsync(requestUri, completionOption, CancellationToken.None);
        }

        /// <summary>Send a GET request to the specified Uri with a cancellation token as an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.</returns>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public Task<HttpResponseMessage> GetAsync(string requestUri, CancellationToken cancellationToken)
        {
            return GetAsync(GetUri(requestUri), cancellationToken);
        }

        /// <summary>Send a GET request to the specified Uri with a cancellation token as an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public Task<HttpResponseMessage> GetAsync(Uri requestUri, CancellationToken cancellationToken)
        {
            return GetAsync(requestUri, HttpCompletionOption.ResponseContentRead, cancellationToken);
        }

        /// <summary>Send a GET request to the specified Uri with an HTTP completion option and a cancellation token as an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.</returns>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <param name="completionOption">An HTTP  completion option value that indicates when the operation should be considered completed.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public Task<HttpResponseMessage> GetAsync(string requestUri, HttpCompletionOption completionOption, CancellationToken cancellationToken)
        {
            return GetAsync(GetUri(requestUri), completionOption, cancellationToken);
        }

        /// <summary>Send a GET request to the specified Uri with an HTTP completion option and a cancellation token as an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <param name="completionOption">An HTTP  completion option value that indicates when the operation should be considered completed.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public Task<HttpResponseMessage> GetAsync(Uri requestUri, HttpCompletionOption completionOption, CancellationToken cancellationToken)
        {
            return SendAsync(new HttpRequestMessage(HttpMethod.Get, requestUri), completionOption, cancellationToken);
        }

        /// <summary>Send a GET request to the specified Uri and return the response body as a byte array in an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public Task<byte[]> GetByteArrayAsync(string requestUri)
        {
            return GetByteArrayAsync(GetUri(requestUri));
        }

        /// <summary>Send a GET request to the specified Uri and return the response body as a byte array in an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public Task<byte[]> GetByteArrayAsync(Uri requestUri)
        {
            //TODO: Look into adding Task.ConfigureAwait (false) here
            //TODO: Test this -- Note there is a similar method in RestBusExtensions
            return SendAsync(new HttpRequestMessage(HttpMethod.Get, requestUri)).ContinueWith<byte[]>(task => task.Result.Content.ReadAsByteArrayAsync().Result);
        }

        /// <summary>Send a GET request to the specified Uri and return the response body as a stream in an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public Task<Stream> GetStreamAsync(string requestUri)
        {
            return GetStreamAsync(GetUri(requestUri));
        }

        /// <summary>Send a GET request to the specified Uri and return the response body as a stream in an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public Task<Stream> GetStreamAsync(Uri requestUri)
        {
            //TODO: Look into adding Task.ConfigureAwait (false) here
            //TODO: Test this -- Note there is a similar method in RestBusExtensions
            return SendAsync(new HttpRequestMessage(HttpMethod.Get, requestUri)).ContinueWith<Stream>(task => task.Result.Content.ReadAsStreamAsync().Result);
        }

        /// <summary>Send a GET request to the specified Uri and return the response body as a string in an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public Task<string> GetStringAsync(string requestUri)
        {
            return GetStringAsync(GetUri(requestUri));
        }

        /// <summary>Send a GET request to the specified Uri and return the response body as a string in an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public Task<string> GetStringAsync(Uri requestUri)
        {
            //TODO: Look into adding Task.ConfigureAwait (false) here
            //TODO: Test this -- Note there is a similar method in RestBusExtensions
            return SendAsync(new HttpRequestMessage(HttpMethod.Get, requestUri)).ContinueWith<string>(task => task.Result.Content.ReadAsStringAsync().Result);
        }

        /// <summary>Send a POST request to the specified Uri as an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <param name="content">The HTTP request content sent to the server.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public Task<HttpResponseMessage> PostAsync(string requestUri, HttpContent content)
        {
            return PostAsync(GetUri(requestUri), content);
        }

        /// <summary>Send a POST request to the specified Uri as an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <param name="content">The HTTP request content sent to the server.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public Task<HttpResponseMessage> PostAsync(Uri requestUri, HttpContent content)
        {
            return PostAsync(requestUri, content, CancellationToken.None);
        }

        /// <summary>Send a POST request with a cancellation token as an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <param name="content">The HTTP request content sent to the server.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public Task<HttpResponseMessage> PostAsync(string requestUri, HttpContent content, CancellationToken cancellationToken)
        {
            return PostAsync(GetUri(requestUri), content, cancellationToken);
        }

        /// <summary>Send a POST request with a cancellation token as an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <param name="content">The HTTP request content sent to the server.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public Task<HttpResponseMessage> PostAsync(Uri requestUri, HttpContent content, CancellationToken cancellationToken)
        {
            return SendAsync(new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = content
            }, cancellationToken);
        }

        /// <summary>Send a PUT request to the specified Uri as an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <param name="content">The HTTP request content sent to the server.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public Task<HttpResponseMessage> PutAsync(string requestUri, HttpContent content)
        {
            return PutAsync(GetUri(requestUri), content);
        }

        /// <summary>Send a PUT request to the specified Uri as an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <param name="content">The HTTP request content sent to the server.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public Task<HttpResponseMessage> PutAsync(Uri requestUri, HttpContent content)
        {
            return PutAsync(requestUri, content, CancellationToken.None);
        }

        /// <summary>Send a PUT request with a cancellation token as an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <param name="content">The HTTP request content sent to the server.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public Task<HttpResponseMessage> PutAsync(string requestUri, HttpContent content, CancellationToken cancellationToken)
        {
            return PutAsync(GetUri(requestUri), content, cancellationToken);
        }

        /// <summary>Send a PUT request with a cancellation token as an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <param name="content">The HTTP request content sent to the server.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public Task<HttpResponseMessage> PutAsync(Uri requestUri, HttpContent content, CancellationToken cancellationToken)
        {
            return SendAsync(new HttpRequestMessage(HttpMethod.Put, requestUri)
            {
                Content = content
            }, cancellationToken);
        }

        /// <summary>Send an HTTP request as an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="request">The HTTP request message to send.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="request" /> was null.</exception>
        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
        {
            return SendAsync(request, HttpCompletionOption.ResponseContentRead, CancellationToken.None);
        }

        /// <summary>Send an HTTP request as an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="request">The HTTP request message to send.</param>
        /// <param name="completionOption">When the operation should complete (as soon as a response is available or after reading the whole response content).</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="request" /> was null.</exception>
        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, HttpCompletionOption completionOption)
        {
            return SendAsync(request, completionOption, CancellationToken.None);
        }

        /// <summary>Send an HTTP request as an asynchronous operation. </summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="request">The HTTP request message to send.</param>
        /// <param name="completionOption">When the operation should complete (as soon as a response is available or after reading the whole response content).</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="request" /> was null.</exception>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, HttpCompletionOption completionOption, CancellationToken cancellationToken)
        {
            return SendAsync(request, cancellationToken);
        }


        #endregion

        public static RequestOptions GetRequestOptions(HttpRequestMessage request)
        {
            object reqObj;
            if (request.Properties.TryGetValue(REQUEST_OPTIONS_KEY, out reqObj))
            {
                return reqObj as RequestOptions;
            }

            return null;
        }

        public static void SetRequestOptions(HttpRequestMessage request, RequestOptions options)
        {
            request.Properties[REQUEST_OPTIONS_KEY] = options;
        }

        private Uri GetUri(string uri)
        {
            if (string.IsNullOrEmpty(uri)) return null;
            return new Uri(uri, UriKind.RelativeOrAbsolute);
        }

    }
}
