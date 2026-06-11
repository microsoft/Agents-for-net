// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.UserAuth.TokenService
{
    /// <summary>
    /// Handles the OAuth callback redirect from Token Service for TeamsTokenServiceAuthorization.
    /// When Token Service redirects the browser to the bot's finalRedirect URL after authentication,
    /// this handler attempts to retrieve the token via GetUserToken and dispatches a proactive
    /// signin/verifyState invoke to resume the conversation.
    /// </summary>
    public class TokenServiceCallbackHandler
    {
        private readonly IStorage _storage;
        private readonly IChannelAdapter _adapter;
        private readonly IAgent _agent;
        private readonly ILogger _logger;

        public TokenServiceCallbackHandler(
            IStorage storage,
            IChannelAdapter adapter,
            IAgent agent,
            ILogger<TokenServiceCallbackHandler> logger = null)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            _agent = agent ?? throw new ArgumentNullException(nameof(agent));
            _logger = logger;
        }

        /// <summary>
        /// Processes the redirect from Token Service after user authentication.
        /// </summary>
        /// <param name="nonce">The state/nonce from the callback query string.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A result indicating whether the token was successfully retrieved.</returns>
        public async Task<TokenServiceCallbackResult> HandleAsync(string nonce, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(nonce))
            {
                return TokenServiceCallbackResult.Failed(400, "Missing state parameter.");
            }

            // Look up the pending state.
            var storageKey = $"teamstokensvc/pending/{nonce}";
            var items = await _storage.ReadAsync([storageKey], cancellationToken).ConfigureAwait(false);
            if (!items.TryGetValue(storageKey, out var stateObj) || stateObj is not TokenServiceCallbackState pendingState)
            {
                return TokenServiceCallbackResult.Failed(400, "Invalid or expired state. Please try signing in again.");
            }

            // Check expiration.
            if (DateTime.UtcNow > pendingState.Expires)
            {
                await _storage.DeleteAsync([storageKey], cancellationToken).ConfigureAwait(false);
                return TokenServiceCallbackResult.Failed(400, "Sign-in request has expired. Please try again.");
            }

            _logger?.LogInformation("Token Service callback received. Attempting GetUserToken for connection '{Connection}'.", pendingState.ConnectionName);

            // Try to get the token from Token Service — if it exchanged the code and stored it,
            // this should return the token without a magic code.
            // NOTE: This is the experiment — we don't know if Token Service makes the token
            // available without the magic code when finalRedirect is used.
            try
            {
                // Dispatch a proactive signin/verifyState invoke to resume the conversation.
                // The auth flow will attempt GetUserToken in context to see if Token Service has the token.
                await DispatchVerifyStateAsync(pendingState, cancellationToken).ConfigureAwait(false);

                // Clean up pending state.
                await _storage.DeleteAsync([storageKey], cancellationToken).ConfigureAwait(false);

                return TokenServiceCallbackResult.Succeeded();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Token Service callback: failed to process.");
                await _storage.DeleteAsync([storageKey], cancellationToken).ConfigureAwait(false);
                return TokenServiceCallbackResult.Failed(500, "An error occurred. Please try again.");
            }
        }

        private async Task DispatchVerifyStateAsync(TokenServiceCallbackState pendingState, CancellationToken cancellationToken)
        {
            var convRef = pendingState.ConversationReference;
            var invokeActivity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = SignInConstants.VerifyStateOperationName,
                // Pass empty state — we expect GetUserToken(null) to work if Token Service stored the token.
                Value = new Dictionary<string, string> { ["state"] = string.Empty },
                ChannelId = convRef.ChannelId,
                ServiceUrl = convRef.ServiceUrl,
                Conversation = convRef.Conversation,
                From = convRef.User,
                Recipient = convRef.Agent
            };

            var identity = AgentClaims.CreateIdentity(pendingState.BotClientId);
            await _adapter.ProcessActivityAsync(identity, (IActivity)invokeActivity, _agent.OnTurnAsync, cancellationToken).ConfigureAwait(false);
        }
    }

    public class TokenServiceCallbackResult
    {
        public bool Success { get; private set; }
        public int StatusCode { get; private set; }
        public string Message { get; private set; }

        public static TokenServiceCallbackResult Succeeded() => new() { Success = true, StatusCode = 200, Message = "Sign-in complete. You may close this window." };
        public static TokenServiceCallbackResult Failed(int statusCode, string message) => new() { Success = false, StatusCode = statusCode, Message = message };
    }

    public class TokenServiceCallbackState : IStoreItem
    {
        public string ConnectionName { get; set; }
        public string BotClientId { get; set; }
        public ConversationReference ConversationReference { get; set; }
        public DateTime Expires { get; set; }
        public string ETag { get; set; }
    }
}
