// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;

namespace Microsoft.Agents.Extensions.A2A;

/// <summary>
/// An A2A-specific <see cref="ITurnContext"/> wrapper that surfaces the current activity as a
/// strongly-typed <see cref="IA2AActivity"/> and exposes A2A-specific helpers.
/// </summary>
public class A2ATurnContext : TurnContextWrapper, IA2ATurnContext
{
    public A2ATurnContext(ITurnContext turnContext) : base(turnContext)
    {
    }

    /// <inheritdoc/>
    public new IA2AActivity Activity => _turnContext.Activity as IA2AActivity;

    /// <inheritdoc/>
    public A2AClient Client => new(_turnContext);
}
