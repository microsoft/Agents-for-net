// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Agents.Core.HeaderPropagation;

/// <summary>
/// Shared context to manage request headers that will be used to propagate them in the <see cref="HeaderPropagationExtensions.AddHeaderPropagation"/>.
/// </summary>
public class HeaderPropagationContext()
{
    private static readonly AsyncLocal<IDictionary<string, StringValues>> _headersFromRequest = new();
    private static Dictionary<string, StringValues> _headersToPropagate = new();

    static HeaderPropagationContext()
    {
        HeaderPropagationAttribute.SetHeadersSerialization();
    }

    /// <summary>
    /// Gets or sets the request headers that will be propagated based on what's inside the <see cref="HeadersToPropagate"/> property.
    /// </summary>
    public static IDictionary<string, StringValues> HeadersFromRequest
    {
        get
        {
            return _headersFromRequest.Value;
        }
        set
        {
            // Create a copy to ensure headers are not modified by the original request.
            var headers = value?.ToDictionary(StringComparer.InvariantCultureIgnoreCase);
            _headersFromRequest.Value = FilterHeaders(headers);
        }
    }

    /// <summary>
    /// Gets or sets the headers to allow during the propagation.
    /// Providing a header with a value will override the one in the request, otherwise the value from the request will be used.
    /// </summary>
    public static IDictionary<string, StringValues> HeadersToPropagate
    {
        get
        {
            var defaultHeaders = new Dictionary<string, StringValues> {
                { "x-ms-correlation-id", "" }
            };
            return defaultHeaders.Concat(_headersToPropagate).ToDictionary(StringComparer.InvariantCultureIgnoreCase);
        }
        set
        {
            _headersToPropagate = _headersToPropagate.Concat(value).ToDictionary(StringComparer.InvariantCultureIgnoreCase);
        }
    }

    /// <summary>
    /// Filters the request headers based on the keys provided in <see cref="HeadersToPropagate"/>.
    /// </summary>
    /// <param name="requestHeaders">Headers collection from an Http request.</param>
    /// <returns>Filtered headers.</returns>
    private static Dictionary<string, StringValues> FilterHeaders(Dictionary<string, StringValues> requestHeaders)
    {
        var result = new Dictionary<string, StringValues>();

        if (requestHeaders == null || requestHeaders.Count == 0)
        {
            return result;
        }

        foreach (var item in HeadersToPropagate)
        {
            if (requestHeaders.TryGetValue(item.Key, out var header))
            {
                var value = !string.IsNullOrWhiteSpace(item.Value) ? item.Value : header;
                result.TryAdd(item.Key, value);
            }
        }

        return result;
    }
}