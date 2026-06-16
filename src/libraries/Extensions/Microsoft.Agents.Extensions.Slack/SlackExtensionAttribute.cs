// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using System;

namespace Microsoft.Agents.Extensions.Slack;

/// <summary>
/// Marks an <see cref="AgentApplication"/> subclass to automatically receive a
/// generated <c>Slack</c> property of type <see cref="SlackAgentExtension"/>.
/// </summary>
/// <remarks>
/// The decorated class must be declared as <c>partial</c>. When the class is compiled, a source
/// generator creates a companion partial class that exposes a <see cref="SlackAgentExtension"/>
/// through a <c>Slack</c> property. The extension is lazily initialized and registered with the
/// application on first access.
/// <code>
/// [SlackExtension]
/// public partial class MyAgent(AgentApplicationOptions options) : AgentApplication(options)
/// {
///     public MyAgent(AgentApplicationOptions options) : base(options)
///     {
///         SlackExtension.OnSlackMessage(OnSlackMessageAsync, rank: RouteRank.Last);
///     }
/// }
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class SlackExtensionAttribute : AgentExtensionAttribute<SlackAgentExtension>
{
}
