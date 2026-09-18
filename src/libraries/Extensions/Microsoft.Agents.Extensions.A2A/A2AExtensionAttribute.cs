// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using System;

namespace Microsoft.Agents.Extensions.A2A;

/// <summary>
/// Marks an <see cref="AgentApplication"/> subclass to automatically receive a
/// generated <c>A2AExtension</c> property of type <see cref="A2AAgentExtension"/>.
/// </summary>
/// <remarks>
/// The decorated class must be declared as <c>partial</c>. When the class is compiled, a source
/// generator creates a companion partial class that exposes a <see cref="A2AAgentExtension"/>
/// through an <c>A2AExtension</c> property. The extension is eagerly initialized and registered
/// during construction via the generated <c>ConfigureExtensions</c> override, ensuring extension
/// handlers are active before the first turn is processed.
/// <code>
/// [A2AExtension]
/// public partial class MyAgent(AgentApplicationOptions options) : AgentApplication(options)
/// {
///     public void ConfigureSkills() =>
///         A2AExtension.Skill("weather", skill => skill.OnMessage(
///             (turnContext, turnState, cancellationToken) => System.Threading.Tasks.Task.CompletedTask));
/// }
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class A2AExtensionAttribute : AgentExtensionAttribute<A2AAgentExtension>
{
}
