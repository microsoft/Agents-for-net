// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.UserAuth.TokenService
{
    /// <summary>
    /// Base class for Agent authentication that handles common logic.
    /// </summary>
    internal class BotUserAuthorization
    {
        private readonly OAuthFlow _flow;
        private readonly OAuthSettings _settings;
        private readonly IStorage _storage;
        private FlowState _state;
        private readonly ClientTokenExchange _dedupe;

        /// <summary>
        /// Name of the authentication handler
        /// </summary>
        protected string _name;

        /// <summary>
        /// Initializes the class
        /// </summary>
        /// <param name="name">The name of authentication handler</param>
        /// <param name="oauthSettings"></param>
        /// <param name="storage"></param>
        public BotUserAuthorization(string name, OAuthSettings oauthSettings, IStorage storage)
        {
            _name = name;
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            _settings = oauthSettings ?? throw new ArgumentNullException(nameof(oauthSettings));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _dedupe = new ClientTokenExchange(_settings, _storage);

            // Subclasses will define the signin prompt so the OAuthFlow optional args aren't needed.
            _flow = new OAuthFlow(oauthSettings);
        }

        /// <summary>
        /// Whether the current activity is a valid activity that supports authentication
        /// </summary>
        /// <param name="context">The turn context</param>
        /// <returns>True if valid. Otherwise, false.</returns>
        public virtual bool IsValidActivity(ITurnContext context)
        {
            // TODO: if flow hasn't started, does it matter what the Activity.Type is?  Though it is likely always an Activity (until it's not).
            var isMatch = context.Activity.Type == ActivityTypes.Message
                && !string.IsNullOrEmpty(context.Activity.Text);

            // TODO: the following is only true if the flow is already started, but we don't know that yet.
            isMatch |= context.Activity.Type == ActivityTypes.Invoke &&
                context.Activity.Name == SignInConstants.VerifyStateOperationName;

            isMatch |= context.Activity.Type == ActivityTypes.Invoke &&
                context.Activity.Name == SignInConstants.TokenExchangeOperationName;

            return isMatch;
        }

        public virtual async Task ResetStateAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            await _storage.DeleteAsync([GetStorageKey(turnContext)], cancellationToken).ConfigureAwait(false);
        }

        private async Task<FlowState> GetFlowStateAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var key = GetStorageKey(turnContext);
            var items = await _storage.ReadAsync([key], cancellationToken).ConfigureAwait(false);
            return items.TryGetValue(key, out object value) ? (FlowState)value : new FlowState();
        }

        private async Task SaveFlowStateAsync(ITurnContext turnContext, FlowState state, CancellationToken cancellationToken)
        {
            var key = GetStorageKey(turnContext);
            var items = new Dictionary<string, object>()
                {
                    { key, state }
                };
            await _storage.WriteAsync(items, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Get a token for the user.
        /// </summary>
        /// <param name="turnContext">The turn context</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The token response if available.</returns>
        public async Task<string> AuthenticateAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            if (_settings.EnableSso && !await _dedupe.DedupeAsync(turnContext, cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            _state = await GetFlowStateAsync(turnContext, cancellationToken).ConfigureAwait(false);

            TokenResponse tokenResponse;
            if (!_state.FlowStarted)
            {
                // If the user is already signed in, tokenResponse will be non-null
                tokenResponse = await OnGetOrStartFlowAsync(turnContext, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // For non-Teams Agents, the user sends the "magic code" that will be used to exchange for a token.
                tokenResponse = await OnContinueFlow(turnContext, cancellationToken);
            }

            await SaveFlowStateAsync(turnContext, _state, cancellationToken).ConfigureAwait(false);

            return tokenResponse?.Token;
        }

        private async Task<TokenResponse> OnGetOrStartFlowAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            // If the user is already signed in, tokenResponse will be non-null
            var tokenResponse = await _flow.BeginFlowAsync(
                turnContext,
                null,
                cancellationToken).ConfigureAwait(false);

            // If a TokenResponse is returned, there was a cached token already.  Otherwise, start the process of getting a new token.
            if (tokenResponse == null)
            {
                var expires = DateTime.UtcNow.AddMilliseconds(_settings.Timeout ?? OAuthSettings.DefaultTimeoutValue.TotalMilliseconds);

                _state.FlowStarted = true;
                _state.FlowExpires = expires;
            }

            return tokenResponse;
        }

        private async Task<TokenResponse> OnContinueFlow(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            TokenResponse tokenResponse;

            _state.ContinueCount++;

            try
            {
                tokenResponse = await _flow.ContinueFlowAsync(turnContext, _state.FlowExpires, cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                throw new AuthException("Authentication flow timed out.", AuthExceptionReason.Timeout);
            }

            if (tokenResponse == null)
            {
                if (_state.ContinueCount >= _settings.InvalidSignInRetryMax)
                {
                    // The only way this happens is if C2 sent a bogus code
                    throw new AuthException("Invalid sign in.", AuthExceptionReason.InvalidSignIn);
                }

                await turnContext.SendActivityAsync(_settings.InvalidSignInRetryMessage, cancellationToken: cancellationToken).ConfigureAwait(false);
                return null;
            }

            _state.FlowStarted = false;
            return tokenResponse;
        }

        private string GetStorageKey(ITurnContext turnContext)
        {
            var channelId = turnContext.Activity.ChannelId ?? throw new InvalidOperationException("invalid activity-missing channelId");
            var conversationId = turnContext.Activity.Conversation?.Id ?? throw new InvalidOperationException("invalid activity-missing Conversation.Id");
            return $"oauth/{_name}/{channelId}/{conversationId}/flowState";
        }
    }

    class FlowState
    {
        public bool FlowStarted = false;
        public DateTime FlowExpires = DateTime.MinValue;
        public int ContinueCount = 0;
    }
}
