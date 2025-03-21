// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.Agents.Hosting.AspNetCore.HeaderPropagation;

/// <summary>
/// Provides configuration for the <see cref="HeaderPropagationContext"/>.
/// </summary>
public class HeaderPropagationOptions
{
    private readonly List<string> _headers = [];

    /// <summary>
    /// Gets or sets the keys of the headers to be propagated from incoming to outgoing requests.
    /// </summary>
    public List<string> Headers => _headers;
}