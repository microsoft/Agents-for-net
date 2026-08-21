// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Core.Models
{
    /// <summary>
    /// Constants related to transport service URLs used by the Agents framework.
    /// </summary>
    public static class TransportConstants
    {
        /// <summary>
        /// The URI prefix for the named pipe transport. Activities whose
        /// <see cref="IActivity.ServiceUrl"/> starts with this value are delivered
        /// over a local named pipe rather than HTTP.
        /// </summary>
        public const string NamedPipeUriPrefix = "urn:botframework:namedpipe:";

        /// <summary>
        /// Returns true when the service URL indicates the named pipe transport.
        /// </summary>
        /// <param name="serviceUrl">The service URL to evaluate. May be null.</param>
        public static bool IsNamedPipeServiceUrl(string serviceUrl)
        {
            return !string.IsNullOrEmpty(serviceUrl)
                && serviceUrl.StartsWith(NamedPipeUriPrefix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns true when the URI is a named pipe transport URI.
        /// </summary>
        /// <param name="uri">The URI to evaluate. May be null.</param>
        public static bool IsNamedPipeUri(Uri uri)
        {
            if (uri == null)
            {
                return false;
            }

            return string.Equals(uri.Scheme, "urn", StringComparison.OrdinalIgnoreCase)
                && uri.AbsoluteUri.StartsWith(NamedPipeUriPrefix, StringComparison.OrdinalIgnoreCase);
        }
    }
}
