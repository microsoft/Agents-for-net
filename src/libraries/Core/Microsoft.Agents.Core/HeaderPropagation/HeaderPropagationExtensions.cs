// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Net.Http;

namespace Microsoft.Agents.Core.HeaderPropagation;

public static class HeaderPropagationExtensions
{
    public static void AddHeaderPropagation(this HttpClient httpClient)
    {
        foreach (var header in HeaderPropagationContext.HeadersFromRequest)
        {
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, [header.Value]);
        }
    }
}
