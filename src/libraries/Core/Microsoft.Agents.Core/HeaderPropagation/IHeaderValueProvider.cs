// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.Agents.Core.HeaderPropagation;

/// <summary>
/// Provides dynamically resolved headers to inject on outgoing HTTP requests.
/// Implementations are registered per-request via <see cref="HeaderPropagationContext.HeaderProviders"/>.
/// </summary>
public interface IHeaderValueProvider
{
    /// <summary>
    /// Returns headers to inject on outgoing requests.
    /// Called each time <see cref="HeaderPropagationExtensions.AddHeaderPropagation"/> executes.
    /// </summary>
    /// <returns>A collection of header entries to apply.</returns>
    IEnumerable<HeaderPropagationEntry> GetHeaders();
}
