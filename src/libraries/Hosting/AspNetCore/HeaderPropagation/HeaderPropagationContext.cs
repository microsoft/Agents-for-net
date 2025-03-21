// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using Microsoft.AspNetCore.Http;

namespace Microsoft.Agents.Hosting.AspNetCore.HeaderPropagation;

/// <summary>
/// Shared context between the <see cref="HeaderPropagationMiddleware"/> and <see cref="AgentsHttpClientFactory"/> for propagating headers.
/// </summary>
public class HeaderPropagationContext(HeaderPropagationOptions options)
{
    private static readonly AsyncLocal<IHeaderDictionary> _headers = new();

    /// <summary>
    /// Gets or sets the headers allowed by the <see cref="HeaderPropagationOptions"/> class to be propagated when the <see cref="HttpClient"/> gets created.
    /// </summary>
    public IHeaderDictionary Headers
    {
        get
        {
            return _headers.Value;
        }
        set
        {
            _headers.Value = FilterHeaders(value);
        }
    }

    /// <summary>
    /// Filters the request headers based on the keys provided in <see cref="HeaderPropagationOptions"/>.
    /// </summary>
    /// <param name="requestHeaders">Headers collection from an Http request.</param>
    /// <returns>Filtered headers.</returns>
    private HeaderDictionary FilterHeaders(IHeaderDictionary requestHeaders)
    {
        var result = new HeaderDictionary();
        if (requestHeaders == null || requestHeaders.Count == 0 || options.Headers.Count == 0)
        {
            return result;
        }

        // Create a copy to ensure headers are not modified by the original request.
        var headers = requestHeaders.ToDictionary(StringComparer.InvariantCultureIgnoreCase);

        foreach (var key in options.Headers)
        {
            if (headers.TryGetValue(key, out var header))
            {
                result.TryAdd(key, header);
            }
        }

        return result;
    }
}