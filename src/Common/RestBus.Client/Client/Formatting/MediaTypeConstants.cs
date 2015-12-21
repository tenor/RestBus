//Sourced (and modified) from https://github.com/mono/aspnetwebstack/blob/master/src/System.Net.Http.Formatting/Formatting/MediaTypeConstants.cs
// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.


namespace RestBus.RabbitMQ.Client.Formatting
{

    using System;
    using System.Net.Http.Headers;
    using System.Text;

    /// <summary>
    /// Constants related to media types.
    /// </summary>
    internal static class MediaTypeConstants
    {
        private static readonly string _defaultApplicationXmlMediaType = "application/xml";
        private static readonly string _defaultTextXmlMediaType = "text/xml";
        private static readonly string _defaultApplicationJsonMediaType = "application/json";
        private static readonly string _defaultTextJsonMediaType = "text/json";
        private static readonly string _defaultApplicationOctetStreamMediaType = "application/octet-stream";
        private static readonly string _defaultApplicationFormUrlEncodedMediaType = "application/x-www-form-urlencoded";

        /// <summary>
        /// Gets a <see cref="MediaTypeHeaderValue"/> instance representing <c>application/octet-stream</c>.
        /// </summary>
        /// <value>
        /// A new <see cref="MediaTypeHeaderValue"/> instance representing <c>application/octet-stream</c>.
        /// </value>
        public static MediaTypeHeaderValue ApplicationOctetStreamMediaType
        {
            get { return new MediaTypeHeaderValue(_defaultApplicationOctetStreamMediaType); }
        }

        /// <summary>
        /// Gets a <see cref="MediaTypeHeaderValue"/> instance representing <c>application/xml</c>.
        /// </summary>
        /// <value>
        /// A new <see cref="MediaTypeHeaderValue"/> instance representing <c>application/xml</c>.
        /// </value>
        public static MediaTypeHeaderValue ApplicationXmlMediaType
        {
            get { return new MediaTypeHeaderValue(_defaultApplicationXmlMediaType); }
        }

        /// <summary>
        /// Gets a <see cref="MediaTypeHeaderValue"/> instance representing <c>application/json</c>.
        /// </summary>
        /// <value>
        /// A new <see cref="MediaTypeHeaderValue"/> instance representing <c>application/json</c>.
        /// </value>
        public static MediaTypeHeaderValue ApplicationJsonMediaType
        {
            get { return new MediaTypeHeaderValue(_defaultApplicationJsonMediaType); }
        }

        /// <summary>
        /// Gets a <see cref="MediaTypeHeaderValue"/> instance representing <c>text/xml</c>.
        /// </summary>
        /// <value>
        /// A new <see cref="MediaTypeHeaderValue"/> instance representing <c>text/xml</c>.
        /// </value>
        public static MediaTypeHeaderValue TextXmlMediaType
        {
            get { return new MediaTypeHeaderValue(_defaultTextXmlMediaType); }
        }

        /// <summary>
        /// Gets a <see cref="MediaTypeHeaderValue"/> instance representing <c>text/json</c>.
        /// </summary>
        /// <value>
        /// A new <see cref="MediaTypeHeaderValue"/> instance representing <c>text/json</c>.
        /// </value>
        public static MediaTypeHeaderValue TextJsonMediaType
        {
            get { return new MediaTypeHeaderValue(_defaultTextJsonMediaType); }
        }

        /// <summary>
        /// Gets a <see cref="MediaTypeHeaderValue"/> instance representing <c>application/x-www-form-urlencoded</c>.
        /// </summary>
        /// <value>
        /// A new <see cref="MediaTypeHeaderValue"/> instance representing <c>application/x-www-form-urlencoded</c>.
        /// </value>
        public static MediaTypeHeaderValue ApplicationFormUrlEncodedMediaType
        {
            get { return new MediaTypeHeaderValue(_defaultApplicationFormUrlEncodedMediaType); }
        }
    }
}
