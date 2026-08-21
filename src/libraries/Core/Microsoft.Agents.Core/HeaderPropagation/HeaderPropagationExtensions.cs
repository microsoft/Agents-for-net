// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Net.Http;

namespace Microsoft.Agents.Core.HeaderPropagation;

public static class HeaderPropagationExtensions
{
    /// <summary>
    /// Loads incoming request headers based on a list of headers to propagate into the HttpClient,
    /// then applies any dynamically resolved headers from registered <see cref="IHeaderValueProvider"/> instances.
    /// </summary>
    /// <param name="httpClient">The <see cref="System.Net.Http.HttpClient"/>.</param>
    public static void AddHeaderPropagation(this HttpClient httpClient)
    {
        // Apply headers captured from the incoming HTTP request.
        if (HeaderPropagationContext.HeadersFromRequest != null)
        {
            foreach (var header in HeaderPropagationContext.HeadersFromRequest)
            {
                httpClient.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, [header.Value]);
            }
        }

        // Apply headers from registered providers (e.g., Activity-derived headers).
        var providers = HeaderPropagationContext.HeaderProviders;
        if (providers != null)
        {
            foreach (var provider in providers)
            {
                foreach (var entry in provider.GetHeaders())
                {
                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation(entry.Key, [entry.Value]);
                }
            }
        }
    }
}
