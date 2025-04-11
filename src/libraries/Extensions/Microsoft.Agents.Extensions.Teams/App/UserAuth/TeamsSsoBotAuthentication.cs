// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.UserAuth;
using Microsoft.Agents.Builder.UserAuth.TokenService;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Storage;
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
    /// Handles authentication for bot in Teams using Teams SSO.
    /// </summary>
    internal class TeamsSsoBotAuthentication
    {
        private readonly Regex _tokenExchangeIdRegex;
        private readonly string _name;
        private readonly IStorage _storage;
        private readonly TeamsSsoSettings _settings;
        private FlowState _state;
        private readonly IConfidentialClientApplicationAdapter _msalAdapter;

        /// <summary>
        /// Initializes the class
        /// </summary>
        /// <param name="name">The name of current authentication handler</param>
        /// <param name="settings">The authentication settings</param>
        /// <param name="storage">The storage to save turn state</param>
        /// <param name="msalAdapter"></param>
        public TeamsSsoBotAuthentication(string name, TeamsSsoSettings settings, IStorage storage, IConfidentialClientApplicationAdapter msalAdapter)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            _name = name;

            _msalAdapter = msalAdapter ?? throw new ArgumentNullException(nameof(msalAdapter));
            _tokenExchangeIdRegex = new Regex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}-" + name);
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        /// <summary>
        /// Whether the current activity is a valid activity that supports authentication
        /// </summary>
        /// <param name="turnContext">The turn context</param>
        /// <returns>True if valid. Otherwise, false.</returns>
        public virtual bool IsValidActivity(ITurnContext turnContext)
        {
            // TODO: this is because SignIn is triggered by a message.  This really shouldn't be the case
            // when Sign in is initiated.  
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
        public async Task<string> AuthenticateAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            if (await ShouldDedupeAsync(turnContext).ConfigureAwait(false))
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
                // For non-Teams bots, the user sends the "magic code" that will be used to exchange for a token.
                tokenResponse = await OnContinueFlow(turnContext, cancellationToken);
            }

            await SaveFlowStateAsync(turnContext, _state, cancellationToken).ConfigureAwait(false);

            return tokenResponse?.Token;
        }

        private async Task<TokenResponse> OnGetOrStartFlowAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            // If the user is already signed in, tokenResponse will be non-null
            var tokenResponse = await _msalAdapter.TryGetUserToken(turnContext, _name, _settings).ConfigureAwait(false);

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

        public virtual async Task ResetStateAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            if (!TryGetStorageKey(turnContext, out var key, out _))
            {
                throw new AuthException("Invalid token exchange Activity.");
            }

            await _storage.DeleteAsync([key], cancellationToken).ConfigureAwait(false);
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
                            Expiration = exchangedToken.ExpiresOn.ToString("o")
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
                    Id = $"{Guid.NewGuid()}-{_name}"
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

        private async Task<bool> ShouldDedupeAsync(ITurnContext context)
        {
            if (!TryGetStorageKey(context, out var key, out var id))
            {
                throw new AuthException("Invalid token exchange Activity.");
            }

            IStoreItem storeItem = new TokenStoreItem(id);
            Dictionary<string, object> storesItems = new()
            {
                {key, storeItem}
            };

            try
            {
                await _storage.WriteAsync(storesItems);
            }
            catch (EtagException)
            {
                return true;
            }

            return false;
        }

        private bool TryGetStorageKey(ITurnContext turnContext, out string key, out string id)
        {
            key = null;
            id = null;

            if (turnContext.Activity.Type != ActivityTypes.Invoke || turnContext.Activity.Name != SignInConstants.TokenExchangeOperationName)
            {
                // TokenExchangeState can only be used with Invokes of signin/tokenExchange
                return false;
            }

            var channelId = turnContext.Activity.ChannelId ?? throw new InvalidOperationException("invalid activity-missing channelId");
            var conversationId = turnContext.Activity.Conversation?.Id ?? throw new InvalidOperationException("invalid activity-missing Conversation.Id");

            var values = ProtocolJsonSerializer.ToJsonElements(turnContext.Activity.Value);
            if (!values.TryGetValue("id", out var valueId))
            {
                // Invalid signin/tokenExchange. Missing activity.value.id
                return false;
            }

            key = $"teamssso/{_name}/{channelId}/{conversationId}/{valueId}/flowState";
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
