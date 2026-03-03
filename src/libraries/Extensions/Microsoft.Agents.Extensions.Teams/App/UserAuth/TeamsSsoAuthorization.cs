// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Authentication.Msal;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.UserAuth;
using Microsoft.Agents.Builder.UserAuth.TokenService;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.App.UserAuth
{
    /// <summary>
    /// Handles authentication based on Teams SSO.
    /// </summary>
    public class TeamsSsoAuthorization : IUserAuthorization
    {
        private readonly ConfidentialClientApplicationAdapter _msalAdapter;
        private readonly IStorage _storage;
        private readonly TeamsSsoSettings _settings;
        private readonly Regex _tokenExchangeIdRegex;
        private FlowState _state;

        public TeamsSsoAuthorization(string name, IStorage storage, IConnections connections, IConfigurationSection configurationSection)
            : this(name, storage, connections, configurationSection.Get<TeamsSsoSettings>())
        {

        }

        /// <summary>
        /// Initialize instance for current class
        /// </summary>
        /// <param name="app">The application.</param>
        /// <param name="name">The authentication name.</param>
        /// <param name="settings">The settings to initialize the class</param>
        /// <param name="connections"></param>
        /// <param name="storage">The storage to use.</param>
        public TeamsSsoAuthorization(string name, IStorage storage, IConnections connections, TeamsSsoSettings settings)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _msalAdapter = GetMsalAdapter(connections);
            _tokenExchangeIdRegex = new Regex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}-" + name);
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public string Name { get; private set; }

        /// <summary>
        /// Sign in current user
        /// </summary>
        /// <param name="turnContext">The turn context</param>
        /// <param name="forceSignIn"></param>
        /// <param name="exchangeConnection"></param>
        /// <param name="exchangeScopes"></param>
        /// <param name="state">The turn state</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The sign in response</returns>
        public async Task<TokenResponse> SignInUserAsync(ITurnContext turnContext, bool forceSignIn, string exchangeConnection, IList<string> exchangeScopes, CancellationToken cancellationToken)
        {
            if (turnContext.Activity.ChannelId != Channels.Msteams)
            {
                throw new InvalidOperationException("TeamsSsoAuthorization only works with Microsoft Teams channel.");
            }

            var token = await _msalAdapter.TryGetUserToken(turnContext, Name, _settings).ConfigureAwait(false);
            if (token != null)
            {
                return new TokenResponse()
                {
                    Token = token.Token
                };
            }

            if (forceSignIn || IsValidActivity(turnContext))
            {
               return new TokenResponse() { Token = await AuthenticateAsync(turnContext, cancellationToken).ConfigureAwait(false) };
            }

            return null;
        }

        public Task<TokenResponse> GetRefreshedUserTokenAsync(ITurnContext turnContext, string exchangeConnection = null, IList<string> exchangeScopes = null, CancellationToken cancellationToken = default)
        {
            // TODO: get silent?
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sign out current user
        /// </summary>
        /// <param name="turnContext">The turn context</param>
        /// <param name="cancellationToken">The cancellation token</param>
        public async Task SignOutUserAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            if (turnContext.Activity.ChannelId != Channels.Msteams)
            {
                throw new InvalidOperationException("TeamsSsoAuthorization only works with Microsoft Teams channel.");
            }

            string homeAccountId = $"{turnContext.Activity.From.AadObjectId}.{turnContext.Activity.Conversation.TenantId}";
            await _msalAdapter.StopLongRunningProcessInWebApiAsync(homeAccountId, cancellationToken);
        }

        public async Task ResetStateAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            if (turnContext.Activity.ChannelId != Channels.Msteams)
            {
                throw new InvalidOperationException("TeamsSsoAuthorization only works with Microsoft Teams channel.");
            }

            if (!TryGetStorageKey(turnContext, out var key, out _))
            {
                throw new AuthException("Invalid token exchange Activity.");
            }

            await _storage.DeleteAsync([key], cancellationToken).ConfigureAwait(false);
        }

        private ConfidentialClientApplicationAdapter GetMsalAdapter(IConnections connections)
        {
            var client = connections.GetConnection(_settings.SsoConnectionName);
            if (client is IMSALProvider msal)
            {
                return new ConfidentialClientApplicationAdapter((IConfidentialClientApplication) msal.CreateClientApplication());
            }

            throw new InvalidOperationException($"Connection '{_settings.SsoConnectionName}' does not support MSAL");
        }

        /// <summary>
        /// Whether the current activity is a valid activity that supports authentication
        /// </summary>
        /// <param name="turnContext">The turn context</param>
        /// <returns>True if valid. Otherwise, false.</returns>
        private bool IsValidActivity(ITurnContext turnContext)
        {
            var isMatch = turnContext.Activity.Type == ActivityTypes.Message
                && !string.IsNullOrEmpty(turnContext.Activity.Text);

            isMatch |= turnContext.Activity.Type == ActivityTypes.Invoke &&
                turnContext.Activity.Name == SignInConstants.VerifyStateOperationName;

            var values = ProtocolJsonSerializer.ToJsonElements(turnContext.Activity.Value);
            if (values.TryGetValue("id", out var id))
            {
                isMatch |= turnContext.Activity.Type == ActivityTypes.Invoke &&
                    turnContext.Activity.Name == SignInConstants.TokenExchangeOperationName
                    && _tokenExchangeIdRegex.IsMatch(id.ToString());
            }

            return isMatch;
        }

        /// <summary>
        /// Get a token for the user.
        /// </summary>
        /// <param name="turnContext">The turn context</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The token response if available.</returns>
        private async Task<string> AuthenticateAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            _state = await GetFlowStateAsync(turnContext, cancellationToken).ConfigureAwait(false);

            TokenResponse tokenResponse;
            if (!_state.FlowStarted)
            {
                // If the user is already signed in, tokenResponse will be non-null
                tokenResponse = await OnGetOrStartFlowAsync(turnContext, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // For non-Teams bots, the user sends the "magic code" that will be used to exchange for a token.
                tokenResponse = await OnContinueFlow(turnContext, cancellationToken);
            }

            await SaveFlowStateAsync(turnContext, _state, cancellationToken).ConfigureAwait(false);

            return tokenResponse?.Token;
        }

        private async Task<TokenResponse> OnGetOrStartFlowAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            // If the user is already signed in, tokenResponse will be non-null
            var tokenResponse = await _msalAdapter.TryGetUserToken(turnContext, Name, _settings).ConfigureAwait(false);

            // If a TokenResponse is returned, there was a cached token already.  Otherwise, start the process of getting a new token.
            if (tokenResponse == null)
            {
                var expires = DateTime.UtcNow.AddMilliseconds(_settings.Timeout ?? OAuthSettings.DefaultTimeoutValue.TotalMilliseconds);

                await SendOAuthCardToObtainTokenAsync(turnContext, cancellationToken).ConfigureAwait(false);

                _state.FlowStarted = true;
                _state.FlowExpires = expires;
            }

            return tokenResponse;
        }

        private async Task<TokenResponse> OnContinueFlow(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            TokenResponse tokenResponse = null;

            _state.ContinueCount++;

            // Check for timeout
            var hasTimedOut = HasTimedOut(turnContext, _state.FlowExpires);
            if (hasTimedOut)
            {
                // if the token fetch request times out, complete the prompt with no result.
                throw new AuthException("Authentication flow timed out.", AuthExceptionReason.Timeout);
            }
            else
            {
                if (IsTeamsVerificationInvoke(turnContext) || IsTokenExchangeRequestInvoke(turnContext))
                {
                    // Recognize token
                    tokenResponse = await RecognizeTokenAsync(turnContext, cancellationToken).ConfigureAwait(false);
                }
                else if (_settings.EndOnInvalidMessage)
                {
                    if (_state.ContinueCount >= _settings.InvalidSignInRetryMax)
                    {
                        // The only way this happens is if C2 sent a bogus code
                        throw new AuthException("Invalid sign in.", AuthExceptionReason.InvalidSignIn);
                    }

                    await turnContext.SendActivityAsync(_settings.InvalidSignInRetryMessage, cancellationToken: cancellationToken).ConfigureAwait(false);
                    return null;
                }
            }

            _state.FlowStarted = false;
            return tokenResponse;
        }

        private static bool HasTimedOut(ITurnContext turnContext, DateTime expires)
        {
            var isMessage = turnContext.Activity.Type == ActivityTypes.Message;

            // If the incoming Activity is a message, or an Activity Type normally handled by OAuthPrompt,
            // check to see if this OAuthPrompt Expiration has elapsed, and end the dialog if so.
            bool isTimeoutActivityType =
              isMessage ||
              IsTeamsVerificationInvoke(turnContext) ||
              IsTokenExchangeRequestInvoke(turnContext);

            return isTimeoutActivityType && DateTime.Compare(DateTime.UtcNow, expires) > 0;
        }

        private static bool IsTeamsVerificationInvoke(ITurnContext context)
        {
            return (context.Activity.Type == ActivityTypes.Invoke) && (context.Activity.Name == SignInConstants.VerifyStateOperationName);
        }
        private static bool IsTokenExchangeRequestInvoke(ITurnContext context)
        {
            return (context.Activity.Type == ActivityTypes.Invoke) && (context.Activity.Name == SignInConstants.TokenExchangeOperationName);
        }

        private async Task<TokenResponse> RecognizeTokenAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            TokenResponse? tokenResponse = null;

            if (IsTeamsVerificationInvoke(turnContext))
            {
                await SendOAuthCardToObtainTokenAsync(turnContext, cancellationToken).ConfigureAwait(false);
                await SendInvokeResponseAsync(turnContext, HttpStatusCode.OK, null, cancellationToken).ConfigureAwait(false);
            }
            else if (IsTokenExchangeRequestInvoke(turnContext))
            {
                var tokenExchangeRequest = turnContext.Activity.Value != null ? ProtocolJsonSerializer.ToObject<TokenExchangeInvokeRequest>(turnContext.Activity.Value) : null;

                // Received activity is not a token exchange request
                if (tokenExchangeRequest == null)
                {
                    string warningMsg =
                      "The bot received an InvokeActivity that is missing a TokenExchangeInvokeRequest value. This is required to be sent with the InvokeActivity.";
                    await SendInvokeResponseAsync(turnContext, HttpStatusCode.BadRequest, warningMsg, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    try
                    {
                        string homeAccountId = $"{turnContext.Activity.From.AadObjectId}.{turnContext.Activity.Conversation.TenantId}";
                        AuthenticationResult exchangedToken = await _msalAdapter.InitiateLongRunningProcessInWebApi(_settings.Scopes, tokenExchangeRequest.Token, ref homeAccountId);

                        tokenResponse = new TokenResponse
                        {
                            Token = exchangedToken.AccessToken,
                            Expiration = exchangedToken.ExpiresOn
                        };

                        await SendInvokeResponseAsync(turnContext, HttpStatusCode.OK, null, cancellationToken).ConfigureAwait(false);
                    }
                    catch (MsalUiRequiredException) // Need user interaction
                    {
                        string warningMsg = "The bot is unable to exchange token. Ask for user consent first.";
                        await SendInvokeResponseAsync(turnContext, HttpStatusCode.PreconditionFailed, new TokenExchangeInvokeResponse
                        {
                            Id = turnContext.Activity.Id,
                            FailureDetail = warningMsg,
                        }, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        string message = $"Failed to get access token with error: {ex.Message}";
                        throw new AuthException(message);
                    }
                }
            }

            return tokenResponse;
        }

        private static async Task SendInvokeResponseAsync(ITurnContext turnContext, HttpStatusCode statusCode, object? body, CancellationToken cancellationToken)
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

        private async Task SendOAuthCardToObtainTokenAsync(ITurnContext context, CancellationToken cancellationToken)
        {
            SignInResource signInResource = GetSignInResource();

            // Ensure prompt initialized
            IActivity prompt = Activity.CreateMessageActivity();
            prompt.Attachments =
            [
                new Attachment
                {
                    ContentType = OAuthCard.ContentType,
                    Content = new OAuthCard
                    {
                        Text = "Sign In",
                        Buttons = new[]
                        {
                                new CardAction
                                {
                                        Title = "Teams SSO Sign In",
                                        Value = signInResource.SignInLink,
                                        Type = ActionTypes.Signin,
                                },
                            },
                        TokenExchangeResource = signInResource.TokenExchangeResource,
                    },
                },
            ];

            // Send prompt
            await context.SendActivityAsync(prompt, cancellationToken).ConfigureAwait(false);
        }

        private SignInResource GetSignInResource()
        {
            string signInLink = $"{_settings.SignInLink}?scope={Uri.EscapeDataString(string.Join(" ", _settings.Scopes))}&clientId={_msalAdapter.AppConfig.ClientId}&tenantId={_msalAdapter.AppConfig.TenantId}";

            SignInResource signInResource = new()
            {
                SignInLink = signInLink,
                TokenExchangeResource = new TokenExchangeResource
                {
                    Id = $"{Guid.NewGuid()}-{Name}"
                }
            };

            return signInResource;
        }


        private async Task<FlowState> GetFlowStateAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            if (!TryGetStorageKey(turnContext, out var key, out _))
            {
                throw new AuthException("Invalid token exchange Activity.");
            }

            var items = await _storage.ReadAsync([key], cancellationToken).ConfigureAwait(false);
            return items.TryGetValue(key, out object value) ? (FlowState)value : new FlowState();
        }

        private async Task SaveFlowStateAsync(ITurnContext turnContext, FlowState state, CancellationToken cancellationToken)
        {
            if (!TryGetStorageKey(turnContext, out var key, out _))
            {
                throw new AuthException("Invalid token exchange Activity.");
            }

            var items = new Dictionary<string, object>()
                {
                    { key, state }
                };
            await _storage.WriteAsync(items, cancellationToken).ConfigureAwait(false);
        }

        private bool TryGetStorageKey(ITurnContext turnContext, out string key, out string id)
        {
            key = null;
            id = null;

            var channelId = turnContext.Activity.ChannelId ?? throw new InvalidOperationException("invalid activity-missing channelId");
            var conversationId = turnContext.Activity.Conversation?.Id ?? throw new InvalidOperationException("invalid activity-missing Conversation.Id");

            key = $"teamssso/{Name}/{channelId}/{conversationId}/flowState";
            return true;
        }
    }

    class FlowState
    {
        public bool FlowStarted = false;
        public DateTime FlowExpires = DateTime.MinValue;
        public int ContinueCount = 0;
    }

    internal class TokenStoreItem : IStoreItem
    {
        public string ETag { get; set; }

        public TokenStoreItem(string etag)
        {
            ETag = etag;
        }
    }
}
