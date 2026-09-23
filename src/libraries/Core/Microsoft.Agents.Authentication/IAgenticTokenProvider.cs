// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Authentication
{
    /// <summary>
    /// Provides access tokens for the agentic identity chain used by Agent 365 / Agent ID scenarios.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Agentic identity is a layered model in which each token in the chain is used to acquire the next:
    /// </para>
    /// <list type="number">
    ///   <item>
    ///     <description>
    ///     <b>Application (blueprint)</b> — a token-exchange credential bound to the agent application
    ///     instance (the federated managed identity). Acquired via
    ///     <see cref="GetAgenticApplicationTokenAsync"/>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///     <b>Instance</b> — an autonomous-agent token, obtained by exchanging the application token.
    ///     Acquired via <see cref="GetAgenticInstanceTokenAsync(string, string, CancellationToken)"/> or
    ///     its scoped overload.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///     <b>Agentic user</b> — a token that represents the agent acting on behalf of a specific agentic
    ///     user. Acquired via <see cref="GetAgenticUserTokenAsync"/>.
    ///     </description>
    ///   </item>
    /// </list>
    /// <para>
    /// Implementations resolve this chain differently. The MSAL-based provider performs each exchange
    /// locally (using <c>api://AzureAdTokenExchange/.default</c> and federated managed identity / FMI
    /// paths), while the Entra Auth Sidecar provider delegates the full chain to an external sidecar
    /// process that owns the agent credential. A connection-level token provider exposes this interface
    /// when it supports agentic flows; callers typically obtain it through <c>AgenticAuthorization</c>.
    /// </para>
    /// </remarks>
    public interface IAgenticTokenProvider
    {
        /// <summary>
        /// Acquires the agentic <b>application (blueprint)</b> token for the specified agent application
        /// instance. This is a token-exchange-scoped token bound to the agent instance via federated
        /// managed identity, and forms the root of the agentic identity chain.
        /// </summary>
        /// <param name="tenantId">The Entra tenant id the agent identity belongs to.</param>
        /// <param name="agentAppInstanceId">
        /// The agent application instance id (the client id of the agent identity / FMI path).
        /// </param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>A task that resolves to the application-level access token.</returns>
        Task<string> GetAgenticApplicationTokenAsync(string tenantId, string agentAppInstanceId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Acquires the agentic <b>instance</b> (autonomous-agent) token for the specified agent
        /// application instance using the provider's default scope.
        /// </summary>
        /// <param name="tenantId">The Entra tenant id the agent identity belongs to.</param>
        /// <param name="agentAppInstanceId">The agent application instance id (the client id of the agent identity).</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>A task that resolves to the instance-level access token.</returns>
        /// <remarks>
        /// The provider performs the application → instance exchange internally, so the caller only needs
        /// to supply the tenant and instance id.
        /// </remarks>
        Task<string> GetAgenticInstanceTokenAsync(string tenantId, string agentAppInstanceId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Acquires the agentic <b>instance</b> (autonomous-agent) token for the specified agent
        /// application instance and the requested scopes.
        /// </summary>
        /// <param name="tenantId">The Entra tenant id the agent identity belongs to.</param>
        /// <param name="agentAppInstanceId">The agent application instance id (the client id of the agent identity).</param>
        /// <param name="scopes">
        /// The scopes to request for the resource the instance token targets. When <see langword="null"/>
        /// or empty, the provider falls back to its configured default scope.
        /// </param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>A task that resolves to the instance-level access token for the requested scopes.</returns>
        Task<string> GetAgenticInstanceTokenAsync(string tenantId, string agentAppInstanceId, IList<string> scopes, CancellationToken cancellationToken = default);

        /// <summary>
        /// Acquires an agentic <b>user</b> token that represents the agent acting on behalf of a specific
        /// agentic user for the requested scopes.
        /// </summary>
        /// <param name="tenantId">The Entra tenant id the agent identity and user belong to.</param>
        /// <param name="agentAppInstanceId">The agent application instance id (the client id of the agent identity).</param>
        /// <param name="upn">
        /// The agentic user to act on behalf of. This may be the user's UPN (user principal name) or the
        /// user's object id (a GUID); implementations select the appropriate identifier based on the value.
        /// </param>
        /// <param name="scopes">
        /// The scopes to request for the resource the user token targets. When <see langword="null"/> or
        /// empty, the provider falls back to its configured default scope.
        /// </param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>A task that resolves to the access token for the agentic user.</returns>
        /// <remarks>
        /// The provider performs the full application → instance → agentic-user chain internally.
        /// </remarks>
        Task<string> GetAgenticUserTokenAsync(string tenantId, string agentAppInstanceId, string upn, IList<string> scopes, CancellationToken cancellationToken = default);
    }
}
