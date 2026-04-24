// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using System;

namespace Microsoft.Agents.Extensions.A2A;

/// <summary>
/// Marks an <see cref="AgentApplication"/> subclass to automatically receive a
/// generated <c>A2A</c> property of type <see cref="A2AAgentExtension"/>.
/// </summary>
/// <remarks>
/// The decorated class must be declared as <c>partial</c>. When the class is compiled, a source
/// generator creates a companion partial class that exposes a <see cref="A2AAgentExtension"/>
/// through a <c>A2A</c> property. The extension is lazily initialized and registered with the
/// application on first access.
/// <code>
/// [A2AExtension]
/// public partial class MyAgent(AgentApplicationOptions options) : AgentApplication(options)
/// {
///     public MyAgent(AgentApplicationOptions options) : base(options)
///     {
///     }
/// }
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class A2AExtensionAttribute : AgentExtensionAttribute<A2AAgentExtension>
{
}
