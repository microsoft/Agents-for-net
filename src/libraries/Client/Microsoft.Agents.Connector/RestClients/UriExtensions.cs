// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Connector.RestClients
{
    internal static class UriExtensions
    {
        public static Uri AppendQuery(this Uri uri, string name, string value, bool escape = true)
        {
            if (value == null)
            {
                return uri;
            }

            var argValue = escape ? Uri.EscapeDataString(value) : value;
            var url = uri.AbsoluteUri;
            if (!url.Contains('?'))
            {
                return new Uri($"{url}?{name}={argValue}");
            }
            else
            {
                return new Uri($"{url}&{name}={argValue}");
            }
        }
    }
}
