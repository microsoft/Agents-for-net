// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.Agents.Hosting.DirectLine.NamedPipes.Protocol
{
    /// <summary>
    /// Represents an incoming request received from the named pipe transport.
    /// </summary>
    internal sealed class NamedPipeRequest
    {
        /// <summary>
        /// Gets or sets the unique identifier for this request.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the HTTP verb (e.g., POST).
        /// </summary>
        public string Verb { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the request path (e.g., /api/messages).
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the content type advertised on <c>Streams[0]</c> (the primary body).
        /// Defaults to <c>application/json</c> when the peer omits it.
        /// </summary>
        public string ContentType { get; set; } = "application/json";

        /// <summary>
        /// Gets or sets the request body bytes.
        /// </summary>
        public byte[] Body { get; set; }

        /// <summary>
        /// Gets or sets the additional attachment streams that traveled alongside this
        /// request (Streams[1..N] on the wire). Empty list when no attachments were sent.
        /// </summary>
        public IList<NamedPipeAttachment> Attachments { get; set; } = [];
    }
}

