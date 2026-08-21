// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Hosting.DirectLine.NamedPipes.Transport
{
    /// <summary>
    /// 48-byte wire frame header for the Bot Framework named pipe protocol.
    /// Format: {Type}.{Length:6}.{Id:36}.{End}\n
    /// </summary>
    internal readonly struct Header
    {
        /// <summary>
        /// Gets the payload type code.
        /// </summary>
        public char Type { get; init; }

        /// <summary>
        /// Gets the length of the payload following this header.
        /// </summary>
        public int PayloadLength { get; init; }

        /// <summary>
        /// Gets the unique identifier for the request/response/stream.
        /// </summary>
        public System.Guid Id { get; init; }

        /// <summary>
        /// Gets a value indicating whether this is the final frame for the given Id.
        /// </summary>
        public bool End { get; init; }
    }

    /// <summary>
    /// Payload type codes used in the named pipe protocol header.
    /// </summary>
    internal static class PayloadTypes
    {
        /// <summary>Request payload type.</summary>
        public const char Request = 'A';

        /// <summary>Response payload type.</summary>
        public const char Response = 'B';

        /// <summary>Stream data payload type.</summary>
        public const char Stream = 'S';

        /// <summary>Cancel all pending operations.</summary>
        public const char CancelAll = 'X';

        /// <summary>Cancel a specific stream.</summary>
        public const char CancelStream = 'C';
    }
}
