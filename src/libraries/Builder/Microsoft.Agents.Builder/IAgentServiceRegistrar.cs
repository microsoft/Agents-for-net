// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// Defines dependency-injection registrations contributed by an Agents SDK extension assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This contract lets an extension register its required services when the host calls
    /// <see cref="AgentServiceCollectionExtensions.AddAgentExtensionServices(IServiceCollection)"/>.
    /// ASP.NET Core hosting calls that method from <c>AddAgentCore</c>; other hosts can call it directly.
    /// The application therefore only needs to reference the extension package rather than invoke a
    /// package-specific service-registration method.
    /// </para>
    /// <para>
    /// An extension opts in by implementing this interface and adding an assembly-level
    /// <see cref="AgentServiceRegistrationAttribute"/>:
    /// </para>
    /// <code>
    /// [assembly: AgentServiceRegistration(typeof(MyExtensionRegistrar))]
    ///
    /// public sealed class MyExtensionRegistrar : IAgentServiceRegistrar
    /// {
    ///     public void ConfigureServices(IServiceCollection services)
    ///     {
    ///         services.TryAddSingleton&lt;MyExtensionService&gt;();
    ///     }
    /// }
    /// </code>
    /// <para>
    /// Registrar types must be public, concrete, and have a public parameterless constructor so consuming
    /// applications can preload and instantiate them. A registrar is invoked at most once for each
    /// <see cref="IServiceCollection"/> instance. Registrars should use the
    /// <c>Microsoft.Extensions.DependencyInjection.Extensions</c> <c>TryAdd</c> methods when application
    /// registrations should take precedence, and must not build or resolve a service provider while
    /// registrations are being configured.
    /// </para>
    /// <para>
    /// A custom host can apply all referenced extension registrations with:
    /// </para>
    /// <code>
    /// services.AddAgentExtensionServices();
    /// </code>
    /// </remarks>
    public interface IAgentServiceRegistrar
    {
        /// <summary>
        /// Adds the services required by the extension to the application service collection.
        /// </summary>
        /// <remarks>
        /// This method runs during service configuration before the application's service provider is built.
        /// Implementations should be idempotent and should not resolve services from the collection.
        /// </remarks>
        /// <param name="services">The service collection being configured.</param>
        void ConfigureServices(IServiceCollection services);
    }
}
