// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Authentication.Msal;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.UserAuth.TeamsAgentic
{
    /// <summary>
    /// Handles the OAuth callback logic for Teams agentic authorization.
    /// This class is transport-agnostic — it accepts an <see cref="OAuthCallbackInput"/>
    /// and returns an <see cref="OAuthCallbackResult"/>, allowing any hosting layer
    /// (ASP.NET Core, Azure Functions, etc.) to act as a thin adapter.
    /// </summary>
    public class TeamsAgenticCallbackHandler
    {
        private readonly IStorage _storage;
        private readonly IConnections _connections;
        private readonly IChannelAdapter _adapter;
        private readonly IAgent _agent;
        private readonly ILogger _logger;

        public TeamsAgenticCallbackHandler(
            IStorage storage,
            IConnections connections,
            IChannelAdapter adapter,
            IAgent agent,
            ILogger<TeamsAgenticCallbackHandler> logger = null)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _connections = connections ?? throw new ArgumentNullException(nameof(connections));
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            _agent = agent ?? throw new ArgumentNullException(nameof(agent));
            _logger = logger;
        }

        /// <summary>
        /// Processes the OAuth callback: validates state, exchanges the authorization code
        /// for a token via MSAL, and dispatches a proactive signin/verifyState invoke
        /// back into the agent's pipeline.
        /// </summary>
        /// <param name="input">The callback parameters extracted by the hosting layer.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A result indicating success or failure with status code and message.</returns>
        public async Task<OAuthCallbackResult> HandleAsync(OAuthCallbackInput input, CancellationToken cancellationToken)
        {
            AssertionHelpers.ThrowIfNull(input, nameof(input));

            // Handle Azure AD error responses.
            if (!string.IsNullOrEmpty(input.Error))
            {
                _logger?.LogWarning("OAuth callback received error: {Error} - {Description}", input.Error, input.ErrorDescription);
                return OAuthCallbackResult.Failed(200, $"{input.Error}: {input.ErrorDescription}");
            }

            // Validate required parameters.
            if (string.IsNullOrEmpty(input.Code) || string.IsNullOrEmpty(input.State))
            {
                return OAuthCallbackResult.Failed(400, "Missing authorization code or state parameter.");
            }

            // Look up the pending OAuth state.
            var storageKey = $"teamsagentic/pending/{input.State}";
            var items = await _storage.ReadAsync([storageKey], cancellationToken).ConfigureAwait(false);
            if (!items.TryGetValue(storageKey, out var stateObj) || stateObj is not OAuthCallbackState pendingState)
            {
                return OAuthCallbackResult.Failed(400, "Invalid or expired state. Please try signing in again.");
            }

            // Check expiration.
            if (DateTime.UtcNow > pendingState.Expires)
            {
                await _storage.DeleteAsync([storageKey], cancellationToken).ConfigureAwait(false);
                return OAuthCallbackResult.Failed(400, "Sign-in request has expired. Please try again.");
            }

            try
            {
                // Exchange the authorization code for tokens via MSAL.
                var connection = _connections.GetConnection(pendingState.ConnectionName);
                if (connection is not IMSALProvider msalProvider)
                {
                    throw new InvalidOperationException($"Connection '{pendingState.ConnectionName}' does not support MSAL.");
                }

                var msalApp = msalProvider.GetOrCreateConfidentialClient(pendingState.RedirectUri);

                var result = await msalApp
                    .AcquireTokenByAuthorizationCode(pendingState.Scopes, input.Code)
                    .WithPkceCodeVerifier(pendingState.CodeVerifier)
                    .ExecuteAsync(cancellationToken)
                    .ConfigureAwait(false);

                // Clean up pending state.
                await _storage.DeleteAsync([storageKey], cancellationToken).ConfigureAwait(false);

                _logger?.LogInformation("OAuth callback: successfully exchanged code for token. HomeAccountId={HomeAccountId}", pendingState.HomeAccountId);

                // Dispatch a proactive signin/verifyState invoke through the agent's pipeline.
                await DispatchVerifyStateAsync(pendingState, result.AccessToken, cancellationToken).ConfigureAwait(false);

                return OAuthCallbackResult.Succeeded();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "OAuth callback: failed to exchange authorization code.");

                // Clean up pending state and notify the agent's pipeline of the failure.
                await _storage.DeleteAsync([storageKey], cancellationToken).ConfigureAwait(false);
                await DispatchSignInFailureAsync(pendingState, ex.Message, cancellationToken).ConfigureAwait(false);

                return OAuthCallbackResult.Failed(500, "An error occurred during sign-in. Please try again.");
            }
        }

        private async Task DispatchVerifyStateAsync(OAuthCallbackState pendingState, string accessToken, CancellationToken cancellationToken)
        {
            var convRef = pendingState.ConversationReference;
            var invokeActivity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = SignInConstants.VerifyStateOperationName,
                Value = new Dictionary<string, string> { ["token"] = accessToken },
                ChannelId = convRef.ChannelId,
                ServiceUrl = convRef.ServiceUrl,
                Conversation = convRef.Conversation,
                From = convRef.User,
                Recipient = convRef.Agent
            };

            var identity = AgentClaims.CreateIdentity(pendingState.BotClientId);
            await _adapter.ProcessActivityAsync(identity, (IActivity)invokeActivity, _agent.OnTurnAsync, cancellationToken).ConfigureAwait(false);
        }

        private async Task DispatchSignInFailureAsync(OAuthCallbackState pendingState, string errorMessage, CancellationToken cancellationToken)
        {
            var convRef = pendingState.ConversationReference;
            var failureActivity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = SignInConstants.SignInFailure,
                Value = new Dictionary<string, string> { ["error"] = errorMessage },
                ChannelId = convRef.ChannelId,
                ServiceUrl = convRef.ServiceUrl,
                Conversation = convRef.Conversation,
                From = convRef.User,
                Recipient = convRef.Agent
            };

            var identity = AgentClaims.CreateIdentity(pendingState.BotClientId);
            await _adapter.ProcessActivityAsync(identity, (IActivity)failureActivity, _agent.OnTurnAsync, cancellationToken).ConfigureAwait(false);
        }
    }
}
