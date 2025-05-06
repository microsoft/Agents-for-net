// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using System;

namespace Microsoft.Agents.Connector
{
    /// <summary>
    /// Extensions for URI's that are used by the Agents Connector Namespace.. 
    /// </summary>
    internal static class UriExtensions
    {
        internal static Uri EnsureTrailingSlash(this Uri uri)
        {

            AssertionHelpers.ThrowIfNull(uri, nameof(uri));
            string uriString = uri.ToString();
#if !NETSTANDARD
            if (!uriString.EndsWith('/'))
#else
            if (!uriString.EndsWith("/"))
#endif
            {
                uriString += "/";
            }
            return new Uri(uriString);

        }
    }
}
