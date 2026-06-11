// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.UserAuth.TokenService
{
    /// <summary>
    /// Teams-specific IUserAuthorization that uses the Azure Bot Token Service with PKCE
    /// to deliver tokens directly (via tokens/response event) without requiring the user
    /// to manually copy a 6-digit magic code.
    ///
    /// Flow:
    /// 1. Calls GetTokenOrSignInResource with a code_challenge
    /// 2. Sends an Adaptive Card with the sign-in link from the SignInResource
    /// 3. Token Service handles PKCE validation on the callback
    /// 4. Token Service sends tokens/response event activity directly to the bot
    /// </summary>
    public class TeamsTokenServiceAuthorization : IUserAuthorization
    {
        private readonly IStorage _storage;
        private readonly OAuthSettings _settings;
        private readonly ILogger _logger;
        private FlowState _state;

        /// <summary>
        /// Required constructor for type loader construction.
        /// </summary>
        public TeamsTokenServiceAuthorization(string name, IStorage storage, IConnections connections, IConfigurationSection configurationSection, ILogger logger = null)
            : this(name, storage, configurationSection.Get<OAuthSettings>(), logger)
        {
        }

        public TeamsTokenServiceAuthorization(string name, IStorage storage, OAuthSettings settings, ILogger logger = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _logger = logger ?? NullLogger<ILogger>.Instance;
        }

        public string Name { get; private set; }

        /// <inheritdoc/>
        public async Task<TokenResponse> SignInUserAsync(ITurnContext turnContext, bool forceSignIn = false, string exchangeConnection = null, IList<string> exchangeScopes = null, CancellationToken cancellationToken = default)
        {
            if (turnContext.Activity.ChannelId != Channels.Msteams)
            {
                throw new InvalidOperationException("TeamsTokenServiceAuthorization only works with Microsoft Teams channel.");
            }

            if (forceSignIn || IsValidActivity(turnContext))
            {
                return await AuthenticateAsync(turnContext, cancellationToken).ConfigureAwait(false);
            }

            return null;
        }

        /// <inheritdoc/>
        public Task<TokenResponse> GetRefreshedUserTokenAsync(ITurnContext turnContext, string exchangeConnection = null, IList<string> exchangeScopes = null, CancellationToken cancellationToken = default)
        {
            // Try to get a cached token from Token Service (no magic code).
            return UserTokenClientWrapper.GetUserTokenAsync(turnContext, _settings.AzureBotOAuthConnectionName, null, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task SignOutUserAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            await UserTokenClientWrapper.SignOutUserAsync(turnContext, _settings.AzureBotOAuthConnectionName, cancellationToken).ConfigureAwait(false);
            await CleanUpFlowAsync(turnContext, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task ResetStateAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            if (TryGetStorageKey(turnContext, out var key))
            {
                await _storage.DeleteAsync([key], cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task<TokenResponse> AuthenticateAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            // Check for tokens/response event — Token Service sent the token directly (PKCE validated).
            if (IsTokenResponseEvent(turnContext))
            {
                _logger.LogInformation("Received tokens/response event — PKCE flow succeeded.");
                var tokenResponse = ProtocolJsonSerializer.ToObject<TokenResponse>(turnContext.Activity.Value);
                await CleanUpFlowAsync(turnContext, cancellationToken).ConfigureAwait(false);
                return tokenResponse;
            }

            // Check for signin/verifyState invoke (fallback path).
            if (IsVerificationInvoke(turnContext))
            {
                var magicCode = GetMagicCodeFromInvoke(turnContext);
                if (!string.IsNullOrEmpty(magicCode))
                {
                    var tokenResponse = await UserTokenClientWrapper.GetUserTokenAsync(
                        turnContext, _settings.AzureBotOAuthConnectionName, magicCode, cancellationToken).ConfigureAwait(false);

                    await SendInvokeResponseAsync(turnContext, tokenResponse != null ? HttpStatusCode.OK : HttpStatusCode.NotFound, null, cancellationToken).ConfigureAwait(false);

                    if (tokenResponse != null)
                    {
                        await CleanUpFlowAsync(turnContext, cancellationToken).ConfigureAwait(false);
                    }

                    return tokenResponse;
                }

                await SendInvokeResponseAsync(turnContext, HttpStatusCode.NotFound, null, cancellationToken).ConfigureAwait(false);
                return null;
            }

            // Try to get an existing token (already authenticated).
            var existingToken = await UserTokenClientWrapper.GetUserTokenAsync(
                turnContext, _settings.AzureBotOAuthConnectionName, null, cancellationToken).ConfigureAwait(false);
            if (existingToken != null)
            {
                return existingToken;
            }

            // No token available — start or continue interactive flow.
            _state = await GetFlowStateAsync(turnContext, cancellationToken).ConfigureAwait(false);

            if (!_state.FlowStarted)
            {
                await SendSignInCardAsync(turnContext, cancellationToken).ConfigureAwait(false);
                _state.FlowStarted = true;
                _state.FlowExpires = DateTime.UtcNow.AddMilliseconds(_settings.Timeout ?? (int)OAuthSettings.DefaultTimeoutValue.TotalMilliseconds);
            }
            else
            {
                // Flow already started but no token yet — check timeout.
                if (DateTime.UtcNow > _state.FlowExpires)
                {
                    await DeleteFlowMessagesAsync(turnContext, cancellationToken).ConfigureAwait(false);
                    await SaveFlowStateAsync(turnContext, _state, cancellationToken).ConfigureAwait(false);
                    throw new AuthException("Authentication flow timed out.", AuthExceptionReason.Timeout);
                }

                _state.ContinueCount++;
                if (_state.ContinueCount >= _settings.InvalidSignInRetryMax)
                {
                    await DeleteFlowMessagesAsync(turnContext, cancellationToken).ConfigureAwait(false);
                    await SaveFlowStateAsync(turnContext, _state, cancellationToken).ConfigureAwait(false);
                    throw new AuthException("Authentication flow exceeded retry limit.", AuthExceptionReason.InvalidSignIn);
                }
            }

            await SaveFlowStateAsync(turnContext, _state, cancellationToken).ConfigureAwait(false);
            return null;
        }

        private async Task SendSignInCardAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            // Generate a nonce to correlate the callback.
            var nonce = Guid.NewGuid().ToString();

            // Build the finalRedirect URL — Token Service will redirect the browser here after auth.
            // The RedirectUri setting should be the bot's callback endpoint (e.g., "https://mybot.azurewebsites.net/auth/callback").
            var finalRedirect = !string.IsNullOrEmpty(_settings.RedirectUri)
                ? $"{_settings.RedirectUri.TrimEnd('/')}?state={nonce}"
                : null;

            // Store pending callback state so the callback handler can resume the conversation.
            if (!string.IsNullOrEmpty(finalRedirect))
            {
                var botAppId = AgentClaims.GetIncomingAudienceClaim(turnContext.Identity);
                var pendingState = new TokenServiceCallbackState
                {
                    ConnectionName = _settings.AzureBotOAuthConnectionName,
                    BotClientId = botAppId,
                    ConversationReference = turnContext.Activity.GetConversationReference(),
                    Expires = DateTime.UtcNow.AddMinutes(10)
                };

                await _storage.WriteAsync(
                    new Dictionary<string, object> { [$"teamstokensvc/pending/{nonce}"] = pendingState },
                    cancellationToken).ConfigureAwait(false);
            }

            // Get sign-in resource from Token Service via the AgentSignIn endpoint (passes code_challenge).
            var userTokenClient = turnContext.Services.Get<IUserTokenClient>();
            var signInResource = await userTokenClient.GetSignInResourceAsync(
                _settings.AzureBotOAuthConnectionName, turnContext.Activity, finalRedirect, cancellationToken).ConfigureAwait(false);

            var signInLink = signInResource?.SignInLink;
            if (string.IsNullOrEmpty(signInLink))
            {
                throw new InvalidOperationException("Token Service did not return a sign-in link.");
            }

            _logger.LogInformation("Sending Adaptive Card with Token Service sign-in link. finalRedirect={FinalRedirect}", finalRedirect ?? "(none)");

            // Send Adaptive Card with sign-in link.
            // OAuthCard doesn't work in Agentic Teams, so we use an Adaptive Card with Action.OpenUrl.
            IActivity prompt = Activity.CreateMessageActivity();
            prompt.Attachments =
            [
                new Attachment
                {
                    ContentType = ContentTypes.AdaptiveCard,
                    Content = new
                    {
                        type = "AdaptiveCard",
                        version = "1.5",
                        body = new object[]
                        {
                            new
                            {
                                type = "TextBlock",
                                text = _settings.Title ?? "Sign In Required",
                                weight = "Bolder",
                                size = "Medium",
                                wrap = true
                            }
                        },
                        actions = new object[]
                        {
                            new
                            {
                                type = "Action.OpenUrl",
                                title = _settings.Text ?? "Sign In",
                                url = signInLink
                            }
                        }
                    }
                },
            ];

            var response = await turnContext.SendActivityAsync(prompt, cancellationToken).ConfigureAwait(false);
            TrackSentActivityId(response);
        }

        private static bool IsValidActivity(ITurnContext turnContext)
        {
            // Message activities
            if (turnContext.Activity.Type == ActivityTypes.Message && !string.IsNullOrEmpty(turnContext.Activity.Text))
            {
                return true;
            }

            // tokens/response event (Token Service sends token directly when PKCE validates)
            if (IsTokenResponseEvent(turnContext))
            {
                return true;
            }

            // signin/verifyState invoke (fallback)
            if (IsVerificationInvoke(turnContext))
            {
                return true;
            }

            return false;
        }

        private static bool IsTokenResponseEvent(ITurnContext turnContext)
        {
            return turnContext.Activity.Type == ActivityTypes.Event
                && string.Equals(turnContext.Activity.Name, SignInConstants.TokenResponseEventName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsVerificationInvoke(ITurnContext turnContext)
        {
            return turnContext.Activity.Type == ActivityTypes.Invoke
                && turnContext.Activity.Name == SignInConstants.VerifyStateOperationName;
        }

        private static string GetMagicCodeFromInvoke(ITurnContext turnContext)
        {
            var values = ProtocolJsonSerializer.ToJsonElements(turnContext.Activity.Value);
            if (values.TryGetValue("state", out var stateElement))
            {
                return stateElement.ToString();
            }

            return null;
        }

        private static async Task SendInvokeResponseAsync(ITurnContext turnContext, HttpStatusCode statusCode, object body, CancellationToken cancellationToken)
        {
            await turnContext.SendActivityAsync(
                new Activity
                {
                    Type = ActivityTypes.InvokeResponse,
                    Value = new InvokeResponse
                    {
                        Status = (int)statusCode,
                        Body = body,
                    },
                }, cancellationToken).ConfigureAwait(false);
        }

        #region Flow State Management

        private void TrackSentActivityId(ResourceResponse response)
        {
            if (_state != null && response != null && !string.IsNullOrEmpty(response.Id))
            {
                _state.SentActivityIds.Add(response.Id);
            }
        }

        private async Task DeleteFlowMessagesAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            if (_state == null || _state.SentActivityIds.Count == 0)
            {
                return;
            }

            foreach (var activityId in _state.SentActivityIds)
            {
                try
                {
                    await turnContext.DeleteActivityAsync(activityId, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    // Best-effort deletion.
                }
            }
        }

        private async Task CleanUpFlowAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            if (!TryGetStorageKey(turnContext, out var key))
            {
                return;
            }

            var items = await _storage.ReadAsync([key], cancellationToken).ConfigureAwait(false);
            if (items.TryGetValue(key, out object value) && value is FlowState state)
            {
                _state = state;
                await DeleteFlowMessagesAsync(turnContext, cancellationToken).ConfigureAwait(false);
            }

            await _storage.DeleteAsync([key], cancellationToken).ConfigureAwait(false);
        }

        private async Task<FlowState> GetFlowStateAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            if (!TryGetStorageKey(turnContext, out var key))
            {
                throw new AuthException("Invalid activity.");
            }

            var items = await _storage.ReadAsync([key], cancellationToken).ConfigureAwait(false);
            return items.TryGetValue(key, out object value) && value is FlowState state ? state : new FlowState();
        }

        private async Task SaveFlowStateAsync(ITurnContext turnContext, FlowState state, CancellationToken cancellationToken)
        {
            if (!TryGetStorageKey(turnContext, out var key))
            {
                throw new AuthException("Invalid activity.");
            }

            await _storage.WriteAsync(
                new Dictionary<string, object> { { key, state } },
                cancellationToken).ConfigureAwait(false);
        }

        private bool TryGetStorageKey(ITurnContext turnContext, out string key)
        {
            var channelId = turnContext.Activity.ChannelId;
            var conversationId = turnContext.Activity.Conversation?.Id;

            if (string.IsNullOrEmpty(channelId) || string.IsNullOrEmpty(conversationId))
            {
                key = null;
                return false;
            }

            key = $"teamstokensvc/{Name}/{channelId}/{conversationId}/flowState";
            return true;
        }

        #endregion

        internal class FlowState : IStoreItem
        {
            public bool FlowStarted { get; set; }
            public DateTime FlowExpires { get; set; } = DateTime.MinValue;
            public int ContinueCount { get; set; }
            public List<string> SentActivityIds { get; set; } = new List<string>();
            public string ETag { get; set; }
        }
    }
}
