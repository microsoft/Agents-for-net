// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Hosting.DirectLine.NamedPipes.Protocol
{
    /// <summary>
    /// Represents a binary stream that travels alongside a request or response payload
    /// (e.g. an attachment uploaded by a DirectLine client). On the wire each attachment
    /// is described by an entry in <see cref="RequestPayload.Streams"/> /
    /// <see cref="ResponsePayload.Streams"/> at index &gt; 0, and its bytes are delivered
    /// as a separately-identified <see cref="PayloadTypes.Stream"/> frame sequence.
    /// </summary>
    internal sealed class NamedPipeAttachment
    {
        /// <summary>
        /// Gets or sets the wire identifier (GUID, "D" format) that links this attachment to
        /// its <see cref="PayloadDescription"/> on the request/response payload.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the MIME content type advertised by the peer.
        /// </summary>
        public string ContentType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the assembled attachment bytes.
        /// </summary>
        public byte[] Body { get; set; }
    }
}
