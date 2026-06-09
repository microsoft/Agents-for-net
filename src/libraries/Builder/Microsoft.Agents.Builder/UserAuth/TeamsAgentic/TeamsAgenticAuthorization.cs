// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Authentication.Msal;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.UserAuth;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.UserAuth.TeamsAgentic
{
    /// <summary>
    /// Handles user authentication for Teams agents running in agentic mode.
    /// Uses a bot-hosted OAuth callback to exchange auth codes and delivers
    /// tokens back via proactive signin/verifyState invoke.
    /// </summary>
    public class TeamsAgenticAuthorization : IUserAuthorization
    {
        private readonly IConnections _connections;
        private readonly IStorage _storage;
        private readonly TeamsAgenticSettings _settings;
        private AgenticFlowState _state;

        /// <summary>
        /// Required constructor for type loader construction.
        /// </summary>
        public TeamsAgenticAuthorization(string name, IStorage storage, IConnections connections, IConfigurationSection configurationSection, ILogger logger = null)
            : this(name, storage, connections, configurationSection.Get<TeamsAgenticSettings>())
        {
        }

        /// <summary>
        /// Initialize instance for current class.
        /// </summary>
        public TeamsAgenticAuthorization(string name, IStorage storage, IConnections connections, TeamsAgenticSettings settings)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        }

        public string Name { get; private set; }

        /// <summary>
        /// Sign in current user.
        /// </summary>
        public async Task<TokenResponse> SignInUserAsync(ITurnContext turnContext, bool forceSignIn, string exchangeConnection, IList<string> exchangeScopes, CancellationToken cancellationToken)
        {
            if (turnContext.Activity.ChannelId != Channels.Msteams)
            {
                throw new InvalidOperationException("TeamsAgenticAuthorization only works with Microsoft Teams channel.");
            }

            if (forceSignIn || IsValidActivity(turnContext))
            {
                return new TokenResponse { Token = await AuthenticateAsync(turnContext, cancellationToken).ConfigureAwait(false) };
            }

            return null;
        }

        public async Task<TokenResponse> GetRefreshedUserTokenAsync(ITurnContext turnContext, string exchangeConnection = null, IList<string> exchangeScopes = null, CancellationToken cancellationToken = default)
        {
            var token = await TryAcquireTokenSilentAsync(turnContext, cancellationToken).ConfigureAwait(false);
            return string.IsNullOrEmpty(token) ? null : new TokenResponse { Token = token };
        }

        /// <summary>
        /// Sign out current user.
        /// </summary>
        public async Task SignOutUserAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            if (turnContext.Activity.ChannelId != Channels.Msteams)
            {
                throw new InvalidOperationException("TeamsAgenticAuthorization only works with Microsoft Teams channel.");
            }

            if (!TryGetStorageKey(turnContext, out var key))
            {
                throw new AuthException("Invalid activity.");
            }

            await _storage.DeleteAsync([key], cancellationToken).ConfigureAwait(false);

            // Remove the user's tokens from MSAL's cache.
            await RemoveMsalAccountAsync(turnContext).ConfigureAwait(false);
        }

        public async Task ResetStateAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            if (turnContext.Activity.ChannelId != Channels.Msteams)
            {
                throw new InvalidOperationException("TeamsAgenticAuthorization only works with Microsoft Teams channel.");
            }

            if (!TryGetStorageKey(turnContext, out var key))
            {
                throw new AuthException("Invalid activity.");
            }

            await _storage.DeleteAsync([key], cancellationToken).ConfigureAwait(false);
        }

        private static IAppConfig GetAppConfig(IConnections connections, string connectionName)
        {
            var client = connections.GetConnection(connectionName);
            if (client is IMSALProvider msal)
            {
                return ((IConfidentialClientApplication)msal.CreateClientApplication()).AppConfig;
            }

            throw new InvalidOperationException($"Connection '{connectionName}' does not support MSAL");
        }

        private IAppConfig GetAppConfigForTurn(ITurnContext turnContext, string connectionName)
        {
            IAccessTokenProvider provider;
            if (!string.IsNullOrEmpty(connectionName))
            {
                provider = _connections.GetConnection(connectionName);
            }
            else
            {
                provider = _connections.GetTokenProvider(turnContext.Identity, turnContext.Activity);
            }

            if (provider is IMSALProvider msal)
            {
                return ((IConfidentialClientApplication)msal.CreateClientApplication()).AppConfig;
            }

            throw new InvalidOperationException("The resolved connection does not support MSAL.");
        }

        private string GetTenantId(ITurnContext turnContext)
        {
            var ssoAppConfig = GetAppConfigForTurn(turnContext, _settings.ConnectionName);
            return ssoAppConfig.TenantId ?? turnContext.Activity.Conversation?.TenantId;
        }

        private static bool IsValidActivity(ITurnContext turnContext)
        {
            var isMatch = turnContext.Activity.Type == ActivityTypes.Message
                && !string.IsNullOrEmpty(turnContext.Activity.Text);

            isMatch |= turnContext.Activity.Type == ActivityTypes.Invoke &&
                turnContext.Activity.Name == SignInConstants.VerifyStateOperationName;

            isMatch |= turnContext.Activity.Type == ActivityTypes.Invoke &&
                turnContext.Activity.Name == SignInConstants.SignInFailure;

            return isMatch;
        }

        private async Task<string> AuthenticateAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            // Try silent token acquisition first (uses MSAL's cached tokens).
            var silentToken = await TryAcquireTokenSilentAsync(turnContext, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(silentToken))
            {
                return silentToken;
            }

            // Silent failed — run the interactive flow.
            _state = await GetFlowStateAsync(turnContext, cancellationToken).ConfigureAwait(false);

            TokenResponse tokenResponse;
            if (!_state.FlowStarted)
            {
                await SendOAuthCardToObtainTokenAsync(turnContext, cancellationToken).ConfigureAwait(false);

                _state.FlowStarted = true;
                _state.FlowExpires = DateTime.UtcNow.AddMilliseconds(_settings.Timeout ?? TimeSpan.FromMinutes(15).TotalMilliseconds);
                tokenResponse = null;
            }
            else
            {
                tokenResponse = await OnContinueFlow(turnContext, cancellationToken);
            }

            await SaveFlowStateAsync(turnContext, _state, cancellationToken).ConfigureAwait(false);

            return tokenResponse?.Token;
        }

        private async Task<string> TryAcquireTokenSilentAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var oauthConnectionName = _settings.OAuthConnectionName ?? _settings.ConnectionName;
            if (string.IsNullOrEmpty(oauthConnectionName) || string.IsNullOrEmpty(_settings.RedirectUri))
            {
                return null;
            }

            var provider = _connections.GetConnection(oauthConnectionName);
            if (provider is not IMSALProvider msalProvider)
            {
                return null;
            }

            var msalApp = msalProvider.GetOrCreateConfidentialClient(_settings.RedirectUri);

            var tenantId = GetTenantId(turnContext);
            var aadObjectId = turnContext.Activity.From?.AadObjectId;
            if (string.IsNullOrEmpty(aadObjectId) || string.IsNullOrEmpty(tenantId))
            {
                return null;
            }

            var homeAccountId = $"{aadObjectId}.{tenantId}";
            var account = await msalApp.GetAccountAsync(homeAccountId).ConfigureAwait(false);
            if (account == null)
            {
                return null;
            }

            try
            {
                var result = await msalApp
                    .AcquireTokenSilent(_settings.Scopes, account)
                    .ExecuteAsync(cancellationToken)
                    .ConfigureAwait(false);

                return result.AccessToken;
            }
            catch (MsalUiRequiredException)
            {
                return null;
            }
        }

        private async Task RemoveMsalAccountAsync(ITurnContext turnContext)
        {
            var oauthConnectionName = _settings.OAuthConnectionName ?? _settings.ConnectionName;
            if (string.IsNullOrEmpty(oauthConnectionName) || string.IsNullOrEmpty(_settings.RedirectUri))
            {
                return;
            }

            var provider = _connections.GetConnection(oauthConnectionName);
            if (provider is not IMSALProvider msalProvider)
            {
                return;
            }

            var msalApp = msalProvider.GetOrCreateConfidentialClient(_settings.RedirectUri);

            var tenantId = GetTenantId(turnContext);
            var aadObjectId = turnContext.Activity.From?.AadObjectId;
            if (string.IsNullOrEmpty(aadObjectId) || string.IsNullOrEmpty(tenantId))
            {
                return;
            }

            var homeAccountId = $"{aadObjectId}.{tenantId}";
            var account = await msalApp.GetAccountAsync(homeAccountId).ConfigureAwait(false);
            if (account != null)
            {
                await msalApp.RemoveAsync(account).ConfigureAwait(false);
            }
        }

        private async Task<TokenResponse> OnContinueFlow(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            _state.ContinueCount++;

            TokenResponse tokenResponse;
            if (HasTimedOut(turnContext, _state.FlowExpires))
            {
                throw new AuthException("Authentication flow timed out.", AuthExceptionReason.Timeout);
            }
            else if (IsSignInFailureInvoke(turnContext))
            {
                throw new AuthException($"Sign in failed: {ProtocolJsonSerializer.ToJson(turnContext.Activity.Value)}", AuthExceptionReason.InvalidSignIn);
            }
            else if (IsTeamsVerificationInvoke(turnContext))
            {
                tokenResponse = RecognizeToken(turnContext);
                await SendInvokeResponseAsync(turnContext, HttpStatusCode.OK, null, cancellationToken).ConfigureAwait(false);

                if (tokenResponse != null)
                {
                    // Flow completed — clean up flow state from storage.
                    if (TryGetStorageKey(turnContext, out var flowKey))
                    {
                        await _storage.DeleteAsync([flowKey], cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            else
            {
                if (_state.ContinueCount >= _settings.InvalidSignInRetryMax)
                {
                    throw new AuthException("Invalid sign in.", AuthExceptionReason.InvalidSignIn);
                }

                await turnContext.SendActivityAsync(_settings.InvalidSignInRetryMessage, cancellationToken: cancellationToken).ConfigureAwait(false);
                return null;
            }

            return tokenResponse;
        }

        private static TokenResponse RecognizeToken(ITurnContext turnContext)
        {
            // The callback endpoint sends a proactive signin/verifyState invoke with the token in the value.
            var values = ProtocolJsonSerializer.ToJsonElements(turnContext.Activity.Value);
            if (values.TryGetValue("token", out var tokenElement))
            {
                return new TokenResponse
                {
                    Token = tokenElement.ToString()
                };
            }

            return null;
        }

        private static bool HasTimedOut(ITurnContext turnContext, DateTime expires)
        {
            var isMessage = turnContext.Activity.Type == ActivityTypes.Message;
            bool isTimeoutActivityType = isMessage || IsTeamsVerificationInvoke(turnContext);
            return isTimeoutActivityType && DateTime.Compare(DateTime.UtcNow, expires) > 0;
        }

        private static bool IsTeamsVerificationInvoke(ITurnContext context)
        {
            return context.Activity.Type == ActivityTypes.Invoke && context.Activity.Name == SignInConstants.VerifyStateOperationName;
        }

        private static bool IsSignInFailureInvoke(ITurnContext context)
        {
            return context.Activity.Type == ActivityTypes.Invoke && context.Activity.Name == SignInConstants.SignInFailure;
        }

        private async Task SendOAuthCardToObtainTokenAsync(ITurnContext context, CancellationToken cancellationToken)
        {
            var ssoAppConfig = GetAppConfigForTurn(context, _settings.ConnectionName);
            var oauthAppConfig = GetAppConfigForTurn(context, _settings.OAuthConnectionName ?? _settings.ConnectionName);

            var tenantId = ssoAppConfig.TenantId ?? context.Activity.Conversation.TenantId;
            var homeAccountId = $"{context.Activity.From.AadObjectId}.{tenantId}";

            // Generate PKCE
            var codeVerifier = GenerateCodeVerifier();
            var codeChallenge = GenerateCodeChallenge(codeVerifier);

            // Generate a nonce to correlate the callback
            var nonce = Guid.NewGuid().ToString();

            // Store pending OAuth state
            var oauthConnectionName = _settings.OAuthConnectionName ?? _settings.ConnectionName;
            if (string.IsNullOrEmpty(oauthConnectionName))
            {
                throw new InvalidOperationException("OAuthConnectionName or ConnectionName must be configured for the OAuth callback to exchange the authorization code.");
            }

            var pendingState = new OAuthCallbackState
            {
                CodeVerifier = codeVerifier,
                HomeAccountId = homeAccountId,
                TenantId = tenantId,
                AuthName = Name,
                Scopes = _settings.Scopes,
                RedirectUri = _settings.RedirectUri,
                ConnectionName = oauthConnectionName,
                BotClientId = ssoAppConfig.ClientId,
                ConversationReference = context.Activity.GetConversationReference(),
                Expires = DateTime.UtcNow.AddMinutes(10)
            };

            await _storage.WriteAsync(
                new Dictionary<string, object> { [$"teamsagentic/pending/{nonce}"] = pendingState },
                cancellationToken).ConfigureAwait(false);

            // Build Azure AD authorize URL
            var scope = Uri.EscapeDataString(string.Join(" ", _settings.Scopes) + " offline_access openid");
            var redirectUri = Uri.EscapeDataString(_settings.RedirectUri);
            var clientId = oauthAppConfig.ClientId;
            var loginHint = context.Activity.From.Name != null
                ? $"&login_hint={Uri.EscapeDataString(context.Activity.From.Name)}"
                : string.Empty;

            var authorizeUrl = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/authorize"
                + $"?client_id={clientId}"
                + $"&response_type=code"
                + $"&redirect_uri={redirectUri}"
                + $"&response_mode=query"
                + $"&scope={scope}"
                + $"&state={nonce}"
                + $"&code_challenge={codeChallenge}"
                + $"&code_challenge_method=S256"
                + loginHint;

            // Send Adaptive Card with sign-in link
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
                                text = "Sign In Required",
                                weight = "Bolder",
                                size = "Medium",
                                wrap = true
                            },
                            new
                            {
                                type = "TextBlock",
                                text = "Click the button below to sign in.",
                                wrap = true
                            }
                        },
                        actions = new object[]
                        {
                            new
                            {
                                type = "Action.OpenUrl",
                                title = "Sign In",
                                url = authorizeUrl
                            }
                        }
                    }
                },
            ];

            await context.SendActivityAsync(prompt, cancellationToken).ConfigureAwait(false);
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

        private async Task<AgenticFlowState> GetFlowStateAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            if (!TryGetStorageKey(turnContext, out var key))
            {
                throw new AuthException("Invalid activity.");
            }

            var items = await _storage.ReadAsync([key], cancellationToken).ConfigureAwait(false);
            return items.TryGetValue(key, out object value) ? (AgenticFlowState)value : new AgenticFlowState();
        }

        private async Task SaveFlowStateAsync(ITurnContext turnContext, AgenticFlowState state, CancellationToken cancellationToken)
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
            var channelId = turnContext.Activity.ChannelId ?? throw new InvalidOperationException("invalid activity-missing channelId");
            var conversationId = turnContext.Activity.Conversation?.Id ?? throw new InvalidOperationException("invalid activity-missing Conversation.Id");

            key = $"teamsagentic/{Name}/{channelId}/{conversationId}/flowState";
            return true;
        }

        private static string GenerateCodeVerifier()
        {
            var bytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static string GenerateCodeChallenge(string codeVerifier)
        {
            var verifierBytes = Encoding.ASCII.GetBytes(codeVerifier);
#if NET8_0_OR_GREATER
            var challengeBytes = SHA256.HashData(verifierBytes);
#else
            using var sha256 = SHA256.Create();
            var challengeBytes = sha256.ComputeHash(verifierBytes);
#endif
            return Convert.ToBase64String(challengeBytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }
    }

    internal class AgenticFlowState : IStoreItem
    {
        public bool FlowStarted = false;
        public DateTime FlowExpires = DateTime.MinValue;
        public int ContinueCount = 0;
        public string ETag { get; set; }
    }
}
