// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Hosting.DirectLine.NamedPipes
{
    /// <summary>
    /// Helpers for identifying request URIs that must be routed through the named pipe transport.
    /// </summary>
    internal static class PipeUriPredicate
    {
        /// <summary>
        /// The required scheme + authority prefix produced by the named pipe ingress.
        /// </summary>
        public const string NamedPipeUriPrefix = TransportConstants.NamedPipeUriPrefix;

        /// <summary>
        /// Returns true when the URI is a well-formed named-pipe service URL that should be
        /// routed through the local pipe transport rather than over HTTP.
        /// </summary>
        /// <param name="uri">The URI to evaluate. May be null.</param>
        public static bool IsNamedPipeUri(Uri uri) => TransportConstants.IsNamedPipeUri(uri);
    }
}
