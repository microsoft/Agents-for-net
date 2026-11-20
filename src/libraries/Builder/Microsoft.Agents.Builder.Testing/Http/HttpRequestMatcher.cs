// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;

namespace Microsoft.Agents.Builder.Testing.Http
{
    internal static class HttpRequestMatcher
    {
        internal static bool Matches(HttpExpectation expectation, HttpRequestMessage request, string clientName)
        {
            var uri = request.RequestUri;
            if (uri == null || !uri.IsAbsoluteUri
                || !string.Equals(expectation.Method.Method, request.Method.Method, StringComparison.Ordinal)
                || !string.Equals(expectation.Uri.Scheme, uri.Scheme, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(expectation.Uri.IdnHost, uri.IdnHost, StringComparison.OrdinalIgnoreCase)
                || expectation.Uri.Port != uri.Port
                || !string.Equals(expectation.Uri.AbsolutePath, uri.AbsolutePath, StringComparison.Ordinal)
                || (expectation.ClientName != null && !string.Equals(expectation.ClientName, clientName, StringComparison.Ordinal)))
            {
                return false;
            }

            var query = ParseQuery(uri.Query);
            if (query.Count != expectation.Query.Count
                || !query.OrderBy(p => p.Key, StringComparer.Ordinal).ThenBy(p => p.Value, StringComparer.Ordinal)
                    .SequenceEqual(expectation.Query.OrderBy(p => p.Key, StringComparer.Ordinal).ThenBy(p => p.Value, StringComparer.Ordinal)))
            {
                return false;
            }

            foreach (var header in expectation.Headers)
            {
                if (!request.Headers.TryGetValues(header.Key, out var values)
                    && (request.Content == null || !request.Content.Headers.TryGetValues(header.Key, out values)))
                {
                    return false;
                }

                var actual = values.ToArray();
                if (!actual.Contains(header.Value, StringComparer.Ordinal)
                    && !string.Equals(string.Join(",", actual), header.Value, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        internal static List<KeyValuePair<string, string>> ParseQuery(string query)
        {
            var result = new List<KeyValuePair<string, string>>();
            foreach (var pair in query.TrimStart('?').Split('&'))
            {
                if (pair.Length == 0)
                {
                    continue;
                }

                var separator = pair.IndexOf('=');
                result.Add(new KeyValuePair<string, string>(
                    Decode(separator < 0 ? pair : pair.Substring(0, separator)),
                    Decode(separator < 0 ? string.Empty : pair.Substring(separator + 1))));
            }

            return result;
        }

        private static string Decode(string value) => Uri.UnescapeDataString(value.Replace("+", " "));
    }
}
