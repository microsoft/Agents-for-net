// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.Agents.Hosting.DirectLine.NamedPipes.Protocol
{
    /// <summary>
    /// Represents a response to send back over the named pipe transport.
    /// </summary>
    internal sealed class NamedPipeResponse
    {
        /// <summary>
        /// Gets or sets the HTTP status code.
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// Gets or sets the content type advertised on <c>Streams[0]</c> (the primary body).
        /// Defaults to <c>application/json</c>; set on outbound responses when sending a non-JSON body.
        /// </summary>
        public string ContentType { get; set; } = "application/json";

        /// <summary>
        /// Gets or sets the response body bytes.
        /// </summary>
        public byte[] Body { get; set; }

        /// <summary>
        /// Gets or sets the additional attachment streams to send alongside the response
        /// (Streams[1..N] on the wire). Null or empty when no attachments are needed.
        /// </summary>
        public IList<NamedPipeAttachment> Attachments { get; set; }

        /// <summary>
        /// Creates a 200 OK response with an optional body.
        /// </summary>
        /// <param name="body">The response body, or null.</param>
        /// <returns>A new <see cref="NamedPipeResponse"/>.</returns>
        public static NamedPipeResponse OK(byte[] body = null) => new() { StatusCode = 200, Body = body };

        /// <summary>
        /// Creates a 202 Accepted response.
        /// </summary>
        /// <returns>A new <see cref="NamedPipeResponse"/>.</returns>
        public static NamedPipeResponse Accepted() => new() { StatusCode = 202 };

        /// <summary>
        /// Creates a 404 Not Found response.
        /// </summary>
        /// <returns>A new <see cref="NamedPipeResponse"/>.</returns>
        public static NamedPipeResponse NotFound() => new() { StatusCode = 404 };

        /// <summary>
        /// Creates a 415 Unsupported Media Type response.
        /// </summary>
        /// <returns>A new <see cref="NamedPipeResponse"/>.</returns>
        public static NamedPipeResponse UnsupportedMediaType() => new() { StatusCode = 415 };

        /// <summary>
        /// Creates a 500 Internal Server Error response.
        /// </summary>
        /// <returns>A new <see cref="NamedPipeResponse"/>.</returns>
        public static NamedPipeResponse InternalServerError() => new() { StatusCode = 500 };
    }
}

