// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using System;

namespace Microsoft.Agents.Extensions.Teams.App;

/// <summary>
/// Marks an <see cref="AgentApplication"/> subclass to automatically receive a
/// generated <c>Teams</c> property of type <see cref="TeamsAgentExtension"/>.
/// </summary>
/// <remarks>
/// The decorated class must be declared as <c>partial</c>. When the class is compiled, a source
/// generator creates a companion partial class that exposes a <see cref="TeamsAgentExtension"/>
/// through a <c>Teams</c> property. The extension is lazily initialized and registered with the
/// application on first access.
/// <code>
/// [TeamsExtension]
/// public partial class MyAgent(AgentApplicationOptions options) : AgentApplication(options)
/// {
///     [ChannelCreatedRoute]
///     public async Task OnChannelCreatedAsync(ITurnContext turnContext, ITurnState turnState,
///         ChannelInfo channelInfo, CancellationToken cancellationToken)
///     {
///         // Teams property is available here
///         _ = Teams;
///     }
/// }
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class TeamsExtensionAttribute : AgentExtensionAttribute<TeamsAgentExtension>
{
}
