// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Builder.App
{
    /// <summary>
    /// Base attribute for automatically generating a lazily-initialized extension property on an
    /// <see cref="AgentApplication"/> subclass.
    /// </summary>
    /// <typeparam name="TExtension">
    /// The <see cref="IAgentExtension"/> type to expose as a generated property.
    /// </typeparam>
    /// <remarks>
    /// <para>
    /// Subclass this attribute to create a named shortcut for a specific extension type.
    /// The decorated class must be declared as <c>partial</c>. A source generator creates a
    /// companion partial class that exposes a <typeparamref name="TExtension"/> property whose
    /// name is derived by stripping the <c>AgentExtension</c> or <c>Extension</c> suffix from the
    /// type name (e.g. <c>TeamsAgentExtension</c> → <c>Teams</c>).
    /// The extension is lazily initialized and registered with the application on first access.
    /// </para>
    /// <para>
    /// Multiple different extension attributes may be applied to the same class.
    /// </para>
    /// <code>
    /// // Define a custom extension attribute:
    /// [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    /// public sealed class MyExtensionAttribute : AgentExtensionAttribute&lt;MyAgentExtension&gt; { }
    ///
    /// // Use it on an agent:
    /// [MyExtension]
    /// public partial class MyAgent(AgentApplicationOptions options) : AgentApplication(options)
    /// {
    ///     // Generated: public MyAgentExtension My { get; }
    /// }
    /// </code>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public class AgentExtensionAttribute<TExtension> : Attribute
        where TExtension : IAgentExtension
    {
    }
}
