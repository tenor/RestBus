using RestBus.Client.Formatting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RestBus.Client
{
    public static class RestBusClientExtensions
    {

        //TODO: Now that a lot of the Formatting source has been moved into the codebase
        //Take a look at the code again and reimplement unimeplemented exceptions, also see if things like Formatting.TryParseDate should be re-coded where it was skipped

        #region Regular Client Method Extensions

        /// <summary>Send a GET request to the specified Uri as an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <param name="options">The options for the request.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public static Task<HttpResponseMessage> GetAsync(this HttpMessageInvoker client, string requestUri, RequestOptions options)
        {
            return GetAsync(client, GetUri(requestUri), options);

        }

        /// <summary>Send a GET request to the specified Uri as an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <param name="options">The options for the request.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public static Task<HttpResponseMessage> GetAsync(this HttpMessageInvoker client, Uri requestUri, RequestOptions options)
        {
            return GetAsync(client, requestUri, CancellationToken.None, options);
        }

        /// <summary>Send a GET request to the specified Uri with a cancellation token as an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.</returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <param name="options">The options for the request.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public static Task<HttpResponseMessage> GetAsync(this HttpMessageInvoker client, string requestUri, CancellationToken cancellationToken, RequestOptions options)
        {
            return GetAsync(client, GetUri(requestUri), cancellationToken, options);
        }

        /// <summary>Send a GET request to the specified Uri with a cancellation token as an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <param name="options">The options for the request.</param> 
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public static Task<HttpResponseMessage> GetAsync(this HttpMessageInvoker client, Uri requestUri, CancellationToken cancellationToken, RequestOptions options)
        {
            if (client == null) throw new ArgumentNullException("client");
            return client.SendAsync(CreateRequest(HttpMethod.Get, requestUri, null, options), cancellationToken);
        }

        /// <summary>Send a POST request to the specified Uri as an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <param name="content">The HTTP request content sent to the server.</param>
        /// <param name="options">The options for the request.</param> 
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public static Task<HttpResponseMessage> PostAsync(this HttpMessageInvoker client, string requestUri, HttpContent content, RequestOptions options)
        {
            return PostAsync(client, GetUri(requestUri), content, options);
        }

        /// <summary>Send a POST request to the specified Uri as an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <param name="content">The HTTP request content sent to the server.</param>
        /// <param name="options">The options for the request.</param> 
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public static Task<HttpResponseMessage> PostAsync(this HttpMessageInvoker client, Uri requestUri, HttpContent content, RequestOptions options)
        {
            return PostAsync(client, requestUri, content, CancellationToken.None, options);
        }

        /// <summary>Send a POST request with a cancellation token as an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <param name="content">The HTTP request content sent to the server.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <param name="options">The options for the request.</param> 
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public static Task<HttpResponseMessage> PostAsync(this HttpMessageInvoker client, string requestUri, HttpContent content, CancellationToken cancellationToken, RequestOptions options)
        {
            return PostAsync(client, GetUri(requestUri), content, cancellationToken, options);
        }

        /// <summary>Send a POST request with a cancellation token as an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <param name="content">The HTTP request content sent to the server.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <param name="options">The options for the request.</param> 
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public static Task<HttpResponseMessage> PostAsync(this HttpMessageInvoker client, Uri requestUri, HttpContent content, CancellationToken cancellationToken, RequestOptions options)
        {
            if (client == null) throw new ArgumentNullException("client");
            return client.SendAsync(CreateRequest(HttpMethod.Post, requestUri, content, options), cancellationToken);
        }

        /// <summary>Send a PUT request to the specified Uri as an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <param name="content">The HTTP request content sent to the server.</param>
        /// <param name="options">The options for the request.</param> 
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public static Task<HttpResponseMessage> PutAsync(this HttpMessageInvoker client, string requestUri, HttpContent content, RequestOptions options)
        {
            return PutAsync(client, GetUri(requestUri), content, options);
        }

        /// <summary>Send a PUT request to the specified Uri as an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <param name="content">The HTTP request content sent to the server.</param>
        /// <param name="options">The options for the request.</param> 
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public static Task<HttpResponseMessage> PutAsync(this HttpMessageInvoker client, Uri requestUri, HttpContent content, RequestOptions options)
        {
            return PutAsync(client, requestUri, content, CancellationToken.None, options);
        }

        /// <summary>Send a PUT request with a cancellation token as an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <param name="content">The HTTP request content sent to the server.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <param name="options">The options for the request.</param> 
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public static Task<HttpResponseMessage> PutAsync(this HttpMessageInvoker client, string requestUri, HttpContent content, CancellationToken cancellationToken, RequestOptions options)
        {
            return PutAsync(client, GetUri(requestUri), content, cancellationToken, options);
        }

        /// <summary>Send a PUT request with a cancellation token as an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <param name="content">The HTTP request content sent to the server.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <param name="options">The options for the request.</param> 
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public static Task<HttpResponseMessage> PutAsync(this HttpMessageInvoker client, Uri requestUri, HttpContent content, CancellationToken cancellationToken, RequestOptions options)
        {
            if (client == null) throw new ArgumentNullException("client");
            return client.SendAsync(CreateRequest(HttpMethod.Put, requestUri, content, options), cancellationToken);
        }

        /// <summary>Send a DELETE request to the specified Uri as an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <param name="options">The options for the request.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public static Task<HttpResponseMessage> DeleteAsync(this HttpMessageInvoker client, string requestUri, RequestOptions options)
        {
            return DeleteAsync(client, GetUri(requestUri), options);
        }

        /// <summary>Send a DELETE request to the specified Uri as an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <param name="options">The options for the request.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public static Task<HttpResponseMessage> DeleteAsync(this HttpMessageInvoker client, Uri requestUri, RequestOptions options)
        {
            return DeleteAsync(client, requestUri, CancellationToken.None, options);
        }

        /// <summary>Send a DELETE request to the specified Uri with a cancellation token as an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <param name="options">The options for the request.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public static Task<HttpResponseMessage> DeleteAsync(this HttpMessageInvoker client, string requestUri, CancellationToken cancellationToken, RequestOptions options)
        {
            return DeleteAsync(client, GetUri(requestUri), cancellationToken, options);
        }

        /// <summary>Send a DELETE request to the specified Uri with a cancellation token as an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <param name="options">The options for the request.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public static Task<HttpResponseMessage> DeleteAsync(this HttpMessageInvoker client, Uri requestUri, CancellationToken cancellationToken, RequestOptions options)
        {
            if (client == null) throw new ArgumentNullException("client");
            return client.SendAsync(CreateRequest(HttpMethod.Delete, requestUri, null, options), cancellationToken);
        }

        /// <summary>Send a GET request to the specified Uri and return the response body as a string in an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <param name="options">The options for the request.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public static Task<string> GetStringAsync(this HttpMessageInvoker client, string requestUri, RequestOptions options)
        {
            return GetStringAsync(client, GetUri(requestUri), options);
        }

        /// <summary>Send a GET request to the specified Uri and return the response body as a string in an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <param name="options">The options for the request.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public static Task<string> GetStringAsync(this HttpMessageInvoker client, Uri requestUri, RequestOptions options)
        {
            //TODO: Look into adding Task.ConfigureAwait (false) here
            return SendAsync(client, new HttpRequestMessage(HttpMethod.Get, requestUri), options)
                .ContinueWith<string>(task => task.Result.Content.ReadAsStringAsync().Result, TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously );

        }

        /// <summary>Send a GET request to the specified Uri and return the response body as a byte array in an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <param name="options">The options for the request.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public static Task<byte[]> GetByteArrayAsync(this HttpMessageInvoker client, string requestUri, RequestOptions options)
        {
            return GetByteArrayAsync(client, GetUri(requestUri), options);
        }

        /// <summary>Send a GET request to the specified Uri and return the response body as a byte array in an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <param name="options">The options for the request.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public static Task<byte[]> GetByteArrayAsync(this HttpMessageInvoker client, Uri requestUri, RequestOptions options)
        {
            //TODO: Look into adding Task.ConfigureAwait (false) here
            return SendAsync(client, new HttpRequestMessage(HttpMethod.Get, requestUri), options)
                .ContinueWith<byte[]>(task => task.Result.Content.ReadAsByteArrayAsync().Result, TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously);

        }

        /// <summary>Send a GET request to the specified Uri and return the response body as a stream in an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <param name="options">The options for the request.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public static Task<Stream> GetStreamAsync(this HttpMessageInvoker client, string requestUri, RequestOptions options)
        {
            return GetStreamAsync(client, GetUri(requestUri), options);
        }

        /// <summary>Send a GET request to the specified Uri and return the response body as a stream in an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <param name="options">The options for the request.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="requestUri" /> was null.</exception>
        public static Task<Stream> GetStreamAsync(this HttpMessageInvoker client, Uri requestUri, RequestOptions options)
        {
            //TODO: Look into adding Task.ConfigureAwait (false) here
            return SendAsync(client, new HttpRequestMessage(HttpMethod.Get, requestUri), options)
                .ContinueWith<Stream>(task => task.Result.Content.ReadAsStreamAsync().Result, TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously);
        }

        /// <summary>Send an HTTP request as an asynchronous operation.</summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="request">The HTTP request message to send.</param>
        /// <param name="options">The options for the request.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="request" /> was null.</exception>
        public static Task<HttpResponseMessage> SendAsync(this HttpMessageInvoker client, HttpRequestMessage request, RequestOptions options)
        {
            return SendAsync(client, request, CancellationToken.None, options);
        }

        /// <summary>Send an HTTP request as an asynchronous operation. </summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="request">The HTTP request message to send.</param>
        /// <param name="cancellationToken">The cancellation token to cancel operation.</param>
        /// <param name="options">The options for the request.</param>
        public static Task<HttpResponseMessage> SendAsync(this HttpMessageInvoker client, HttpRequestMessage request, CancellationToken cancellationToken, RequestOptions options)
        {
            if (client == null) throw new ArgumentNullException("client");
            AttachOptions(request, options);
            return client.SendAsync(request, cancellationToken);
        }

        #endregion

        #region Formatted Client Method Extensions

        /// <summary>Sends a POST request as an asynchronous operation, with a specified value serialized as JSON.</summary>
        /// <returns>A task object representing the asynchronous operation.</returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The URI the request is sent to.</param>
        /// <param name="value">The value to write into the entity body of the request.</param>
        /// <param name="options">The options for the request.</param>
        /// <typeparam name="T">The type of object to serialize.</typeparam>
        public static Task<HttpResponseMessage> PostAsJsonAsync<T>(this HttpMessageInvoker client, string requestUri, T value, RequestOptions options)
        {
            return client.PostAsJsonAsync(requestUri, value, CancellationToken.None, options);
        }

        /// <summary>Sends a POST request as an asynchronous operation, with a specified value serialized as JSON. Includes a cancellation token to cancel the request.</summary>
        /// <returns>A task object representing the asynchronous operation.</returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The URI the request is sent to.</param>
        /// <param name="value">The value to write into the entity body of the request.</param>
        /// <param name="options">The options for the request.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <typeparam name="T">The type of object to serialize.</typeparam>
        public static Task<HttpResponseMessage> PostAsJsonAsync<T>(this HttpMessageInvoker client, string requestUri, T value, CancellationToken cancellationToken, RequestOptions options)
        {
            return client.PostAsync(requestUri, value, new JsonMediaTypeFormatter(), cancellationToken, options);
        }

        /// <summary>Sends a POST request as an asynchronous operation, with a specified value serialized as XML.</summary>
        /// <returns>A task object representing the asynchronous operation.</returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The URI the request is sent to.</param>
        /// <param name="value">The value to write into the entity body of the request.</param>
        /// <param name="options">The options for the request.</param>
        /// <typeparam name="T">The type of object to serialize.</typeparam>
        public static Task<HttpResponseMessage> PostAsXmlAsync<T>(this HttpMessageInvoker client, string requestUri, T value, RequestOptions options)
        {
            return client.PostAsXmlAsync(requestUri, value, CancellationToken.None, options);
        }

        /// <summary>Sends a POST request as an asynchronous operation, with a specified value serialized as XML. Includes a cancellation token to cancel the request.</summary>
        /// <returns>A task object representing the asynchronous operation.</returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The URI the request is sent to.</param>
        /// <param name="value">The value to write into the entity body of the request.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <param name="options">The options for the request.</param>
        /// <typeparam name="T">The type of object to serialize.</typeparam>
        public static Task<HttpResponseMessage> PostAsXmlAsync<T>(this HttpMessageInvoker client, string requestUri, T value, CancellationToken cancellationToken, RequestOptions options)
        {
            return client.PostAsync(requestUri, value, new XmlMediaTypeFormatter(), cancellationToken, options);
        }

        /// <summary>Sends a POST request as an asynchronous operation, with a specified value serialized using the given formatter.</summary>
        /// <returns>A task object representing the asynchronous operation.</returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The URI the request is sent to.</param>
        /// <param name="value">The value to write into the entity body of the request.</param>
        /// <param name="formatter">The formatter used to serialize the value.</param>
        /// <param name="options">The options for the request.</param>
        /// <typeparam name="T">The type of object to serialize.</typeparam>
        private static Task<HttpResponseMessage> PostAsync<T>(this HttpMessageInvoker client, string requestUri, T value, MediaTypeFormatter formatter, RequestOptions options)
        {
            return client.PostAsync(requestUri, value, formatter, CancellationToken.None, options);
        }

        /// <summary>Sends a POST request as an asynchronous operation, with a specified value serialized using the given formatter. Includes a cancellation token to cancel the request.</summary>
        /// <returns>A task object representing the asynchronous operation.</returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The URI the request is sent to.</param>
        /// <param name="value">The value to write into the entity body of the request.</param>
        /// <param name="formatter">The formatter used to serialize the value.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <param name="options">The options for the request.</param>
        /// <typeparam name="T">The type of object to serialize.</typeparam>
        private static Task<HttpResponseMessage> PostAsync<T>(this HttpMessageInvoker client, string requestUri, T value, MediaTypeFormatter formatter, CancellationToken cancellationToken, RequestOptions options)
        {
            MediaTypeHeaderValue mediaType = null;
            return client.PostAsync(requestUri, value, formatter, mediaType, cancellationToken, options);
        }

        /// <summary>Sends a POST request as an asynchronous operation, with a specified value serialized using the given formatter and media type string.</summary>
        /// <returns>A task object representing the asynchronous operation.</returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The URI the request is sent to.</param>
        /// <param name="value">The value to write into the entity body of the request.</param>
        /// <param name="formatter">The formatter used to serialize the value.</param>
        /// <param name="mediaType">The authoritative value of the Content-Type header. Can be null, in which case the  default content type of the formatter will be used.</param>
        /// <param name="options">The options for the request.</param>
        /// <typeparam name="T">The type of object to serialize.</typeparam>
        private static Task<HttpResponseMessage> PostAsync<T>(this HttpMessageInvoker client, string requestUri, T value, MediaTypeFormatter formatter, string mediaType, RequestOptions options)
        {
            return client.PostAsync(requestUri, value, formatter, mediaType, CancellationToken.None, options);
        }

        /// <summary>Sends a POST request as an asynchronous operation, with a specified value serialized using the given formatter and media type string. Includes a cancellation token to cancel the request.</summary>
        /// <returns>A task object representing the asynchronous operation.</returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The URI the request is sent to.</param>
        /// <param name="value">The value to write into the entity body of the request.</param>
        /// <param name="formatter">The formatter used to serialize the value.</param>
        /// <param name="mediaType">The authoritative value of the Content-Type header. Can be null, in which case the  default content type of the formatter will be used.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <param name="options">The options for the request.</param>
        /// <typeparam name="T">The type of object to serialize.</typeparam>
        private static Task<HttpResponseMessage> PostAsync<T>(this HttpMessageInvoker client, string requestUri, T value, MediaTypeFormatter formatter, string mediaType, CancellationToken cancellationToken, RequestOptions options)
        {
            return client.PostAsync(requestUri, value, formatter, ObjectContent.BuildHeaderValue(mediaType), cancellationToken, options);
        }

        /// <summary>Sends a POST request as an asynchronous operation, with a specified value serialized using the given formatter and media type.</summary>
        /// <returns>A task object representing the asynchronous operation.</returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The URI the request is sent to.</param>
        /// <param name="value">The value to write into the entity body of the request.</param>
        /// <param name="formatter">The formatter used to serialize the value.</param>
        /// <param name="mediaType">The authoritative value of the Content-Type header. Can be null, in which case the  default content type of the formatter will be used.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <param name="options">The options for the request.</param>
        /// <typeparam name="T">The type of object to serialize.</typeparam>
        private static Task<HttpResponseMessage> PostAsync<T>(this HttpMessageInvoker client, string requestUri, T value, MediaTypeFormatter formatter, MediaTypeHeaderValue mediaType, CancellationToken cancellationToken, RequestOptions options)
        {
            if (client == null) throw new ArgumentNullException("client");
            ObjectContent<T> objectContent = new ObjectContent<T>(value, formatter, mediaType);
            return client.PostAsync(requestUri, objectContent, cancellationToken, options);
        }

        /// <summary>Sends a PUT request as an asynchronous operation, with a specified value serialized as JSON.</summary>
        /// <returns>A task object representing the asynchronous operation.</returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The URI the request is sent to.</param>
        /// <param name="value">The value to write into the entity body of the request.</param>
        /// <param name="options">The options for the request.</param>
        /// <typeparam name="T">The type of object to serialize.</typeparam>
        public static Task<HttpResponseMessage> PutAsJsonAsync<T>(this HttpMessageInvoker client, string requestUri, T value, RequestOptions options)
        {
            return client.PutAsJsonAsync(requestUri, value, CancellationToken.None, options);
        }

        /// <summary>Sends a PUT request as an asynchronous operation, with a specified value serialized as JSON. Includes a cancellation token to cancel the request.</summary>
        /// <returns>A task object representing the asynchronous operation.</returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The URI the request is sent to.</param>
        /// <param name="value">The value to write into the entity body of the request.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation. </param>
        /// <param name="options">The options for the request.</param>
        /// <typeparam name="T">The type of object to serialize.</typeparam>
        public static Task<HttpResponseMessage> PutAsJsonAsync<T>(this HttpMessageInvoker client, string requestUri, T value, CancellationToken cancellationToken, RequestOptions options)
        {
            return client.PutAsync(requestUri, value, new JsonMediaTypeFormatter(), cancellationToken, options);
        }

        /// <summary>Sends a PUT request as an asynchronous operation, with a specified value serialized as XML.</summary>
        /// <returns>A task object representing the asynchronous operation.</returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The URI the request is sent to.</param>
        /// <param name="value">The value to write into the entity body of the request.</param>
        /// <param name="options">The options for the request.</param>
        /// <typeparam name="T">The type of object to serialize.</typeparam>
        public static Task<HttpResponseMessage> PutAsXmlAsync<T>(this HttpMessageInvoker client, string requestUri, T value, RequestOptions options)
        {
            return client.PutAsXmlAsync(requestUri, value, CancellationToken.None, options);
        }

        /// <summary>Sends a PUT request as an asynchronous operation, with a specified value serialized as XML. Includes a cancellation token to cancel the request.</summary>
        /// <returns>A task object representing the asynchronous operation.</returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The URI the request is sent to.</param>
        /// <param name="value">The value to write into the entity body of the request.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <param name="options">The options for the request.</param>
        /// <typeparam name="T">The type of object to serialize.</typeparam>
        public static Task<HttpResponseMessage> PutAsXmlAsync<T>(this HttpMessageInvoker client, string requestUri, T value, CancellationToken cancellationToken, RequestOptions options)
        {
            return client.PutAsync(requestUri, value, new XmlMediaTypeFormatter(), cancellationToken, options);
        }

        /// <summary>Sends a PUT request as an asynchronous operation, with a specified value serialized using the given formatter.</summary>
        /// <returns>A task object representing the asynchronous operation.</returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The URI the request is sent to.</param>
        /// <param name="value">The value to write into the entity body of the request.</param>
        /// <param name="formatter">The formatter used to serialize the value.</param>
        /// <param name="options">The options for the request.</param>
        /// <typeparam name="T">The type of object to serialize.</typeparam>
        private static Task<HttpResponseMessage> PutAsync<T>(this HttpMessageInvoker client, string requestUri, T value, MediaTypeFormatter formatter, RequestOptions options)
        {
            return client.PutAsync(requestUri, value, formatter, CancellationToken.None, options);
        }

        /// <summary>Sends a PUT request as an asynchronous operation, with a specified value serialized using the given formatter and medai type string. Includes a cancellation token to cancel the request.</summary>
        /// <returns>A task object representing the asynchronous operation.</returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The URI the request is sent to.</param>
        /// <param name="value">The value to write into the entity body of the request.</param>
        /// <param name="formatter">The formatter used to serialize the value.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <param name="options">The options for the request.</param>
        /// <typeparam name="T">The type of object to serialize.</typeparam>
        private static Task<HttpResponseMessage> PutAsync<T>(this HttpMessageInvoker client, string requestUri, T value, MediaTypeFormatter formatter, CancellationToken cancellationToken, RequestOptions options)
        {
            MediaTypeHeaderValue mediaType = null;
            return client.PutAsync(requestUri, value, formatter, mediaType, cancellationToken, options);
        }

        /// <summary>Sends a PUT request as an asynchronous operation, with a specified value serialized using the given formatter and media type string.</summary>
        /// <returns>A task object representing the asynchronous operation.</returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The URI the request is sent to.</param>
        /// <param name="value">The value to write into the entity body of the request.</param>
        /// <param name="formatter">The formatter used to serialize the value.</param>
        /// <param name="mediaType">The authoritative value of the Content-Type header. Can be null, in which case the  default content type of the formatter will be used.</param>
        /// <param name="options">The options for the request.</param>
        /// <typeparam name="T">The type of object to serialize.</typeparam>
        private static Task<HttpResponseMessage> PutAsync<T>(this HttpMessageInvoker client, string requestUri, T value, MediaTypeFormatter formatter, string mediaType, RequestOptions options)
        {
            return client.PutAsync(requestUri, value, formatter, mediaType, CancellationToken.None, options);
        }

        /// <summary>Sends a PUT request as an asynchronous operation, with a specified value serialized using the given formatter and media type string. Includes a cancellation token to cancel the request.</summary>
        /// <returns>A task object representing the asynchronous operation.</returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The URI the request is sent to.</param>
        /// <param name="value">The value to write into the entity body of the request.</param>
        /// <param name="formatter">The formatter used to serialize the value.</param>
        /// <param name="mediaType">The authoritative value of the Content-Type header. Can be null, in which case the  default content type of the formatter will be used.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <param name="options">The options for the request.</param>
        /// <typeparam name="T">The type of object to serialize.</typeparam>
        private static Task<HttpResponseMessage> PutAsync<T>(this HttpMessageInvoker client, string requestUri, T value, MediaTypeFormatter formatter, string mediaType, CancellationToken cancellationToken, RequestOptions options)
        {
            return client.PutAsync(requestUri, value, formatter, ObjectContent.BuildHeaderValue(mediaType), cancellationToken, options);
        }

        /// <summary> Sends a PUT request as an asynchronous operation, with a specified value serialized using the given formatter and media type. Includes a cancellation token to cancel the request.</summary>
        /// <returns>A task object representing the asynchronous operation.</returns>
        /// <param name="client">The client used to make the request.</param>
        /// <param name="requestUri">The URI the request is sent to.</param>
        /// <param name="value">The value to write into the entity body of the request.</param>
        /// <param name="formatter">The formatter used to serialize the value.</param>
        /// <param name="mediaType">The authoritative value of the Content-Type header. Can be null, in which case the  default content type of the formatter will be used.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <param name="options">The options for the request.</param>
        /// <typeparam name="T">The type of object to serialize.</typeparam>
        private static Task<HttpResponseMessage> PutAsync<T>(this HttpMessageInvoker client, string requestUri, T value, MediaTypeFormatter formatter, MediaTypeHeaderValue mediaType, CancellationToken cancellationToken, RequestOptions options)
        {
            if (client == null) throw new ArgumentNullException("client");

            ObjectContent<T> objectContent = new ObjectContent<T>(value, formatter, mediaType);
            return client.PutAsync(requestUri, objectContent, cancellationToken, options);
        }

        #endregion

        #region Helper Methods
        static Uri GetUri(string uri)
        {
            if (string.IsNullOrEmpty(uri))
            {
                return null;
            }
            return new Uri(uri, UriKind.RelativeOrAbsolute);
        }

        static HttpRequestMessage CreateRequest(HttpMethod method, Uri uri, HttpContent content, RequestOptions options)
        {
            var request = new HttpRequestMessage(method, uri) { Content = content };
            AttachOptions(request, options);
            return request;

        }

        private static void AttachOptions(HttpRequestMessage request, RequestOptions options)
        {
            if (options != null && options.Headers != null)
            {
                //Copy headers over

                foreach (var header in options.Headers)
                {
                    if (request.Headers.Contains(header.Key))
                    {
                        request.Headers.Remove(header.Key);
                    }
                    request.Headers.Add(header.Key, header.Value);
                }
            }

            //Attach options to request
            RestBusClient.SetRequestOptions(request, options);
        }


        #endregion

    }
}
