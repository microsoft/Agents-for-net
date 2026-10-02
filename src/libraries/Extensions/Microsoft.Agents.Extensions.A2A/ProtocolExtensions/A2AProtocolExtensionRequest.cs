// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Agents.Extensions.A2A.ProtocolExtensions;

internal sealed class A2AProtocolExtensionRequest
{
    internal const string HeaderName = "A2A-Extensions";
    private readonly HashSet<string> _activated;

    private A2AProtocolExtensionRequest(IEnumerable<string> activated)
    {
        _activated = new HashSet<string>(activated, StringComparer.Ordinal);
    }

    internal bool IsActivated(string uri) => _activated.Contains(uri);

    internal static A2AProtocolExtensionRequest Create(HttpRequest request)
    {
        var activated = request.Headers[HeaderName]
            .SelectMany(value => value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
        return new A2AProtocolExtensionRequest(activated);
    }
}
