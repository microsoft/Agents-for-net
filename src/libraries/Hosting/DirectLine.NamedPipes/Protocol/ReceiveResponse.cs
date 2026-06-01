// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.Agents.Hosting.DirectLine.NamedPipes.Protocol
{
    /// <summary>
    /// Represents an incoming response received from the named pipe transport
    /// (for outbound requests sent by the agent).
    /// </summary>
    internal sealed class ReceiveResponse
    {
        /// <summary>
        /// Gets or sets the HTTP status code.
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// Gets or sets the content type advertised on <c>Streams[0]</c> (the primary body).
        /// Defaults to <c>application/json</c> when the peer omits it.
        /// </summary>
        public string ContentType { get; set; } = "application/json";

        /// <summary>
        /// Gets or sets the response body bytes.
        /// </summary>
        public byte[] Body { get; set; }

        /// <summary>
        /// Gets or sets the additional attachment streams that traveled alongside this
        /// response (Streams[1..N] on the wire). Empty list when no attachments were sent.
        /// </summary>
        public IList<NamedPipeAttachment> Attachments { get; set; } = [];
    }
}

