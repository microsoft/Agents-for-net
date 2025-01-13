// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.Storage;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Interfaces;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Connector;

namespace AuthenticationBot
{
    public class ActivityHandlerWithOAuth : ActivityHandler
    {
        private readonly OAuthFlow _flow;
        private FlowState _flowState;
        private readonly IStorage _storage;
        private OAuthSettings _settings;

        public class OAuthSettings
        {
            public string ConnectionName { get; set; }
            public string Title { get; set; } = "Sign In";
            public string Text { get; set; } = "Please sign in and send 6-digit code";
            public int Timeout { get; set; } = 60000;
            public bool? ShowSignInLink { get; set; }
            public string TimeoutMessage { get; set; }
            public bool AutoRetry { get; set; } = true;

            public bool IsOAuthEnabled => !string.IsNullOrWhiteSpace(ConnectionName);
        }

        public ActivityHandlerWithOAuth(
            IStorage storage,
            OAuthSettings oAuthSettings = null)
        {
            _settings = oAuthSettings ?? new OAuthSettings();

            if (_settings.IsOAuthEnabled)
            {
                _storage = storage ?? throw new ArgumentNullException(nameof(storage));
                _flow = new OAuthFlow(_settings.Title, _settings.Text, _settings.ConnectionName, _settings.Timeout, _settings.ShowSignInLink);
            }
        }

        protected async Task<TokenResponse> SigninUserAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            if (!_settings.IsOAuthEnabled)
            {
                throw new InvalidOperationException("SigninUserAsync requires a connection name");
            }

            TokenResponse tokenResponse;

            if (!_flowState.FlowStarted)
            {
                tokenResponse = await _flow.BeginFlowAsync(turnContext, null, cancellationToken);

                // If a TokenResponse is returned, there was a cached token already.  Otherwise, start the process of getting a new token.
                if (tokenResponse == null)
                {
                    var expires = DateTime.UtcNow.AddMilliseconds(_flow.Timeout ?? TimeSpan.FromMinutes(15).TotalMilliseconds);

                    _flowState.FlowStarted = true;
                    _flowState.FlowExpires = expires;
                }
            }
            else
            {
                tokenResponse = await OnContinueFlow(turnContext, cancellationToken).ConfigureAwait(false);
            }

            return tokenResponse;
        }

        protected async Task SignOutUserAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            if (!_settings.IsOAuthEnabled)
            {
                throw new InvalidOperationException("SignOutUserAsync requires a connection name");
            }

            await _flow.SignOutUserAsync(turnContext, cancellationToken);
        }

        public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            if (!_settings.IsOAuthEnabled)
            {
                await base.OnTurnAsync(turnContext, cancellationToken);
                return;
            }

            if (ShouldExchange(turnContext))
            {
                // If the TokenExchange is NOT successful, the response will have already been sent by ExchangedTokenAsync
                if (!await ExchangedTokenAsync(turnContext, cancellationToken).ConfigureAwait(false))
                {
                    // do not process this activity further.
                    return;
                }

                // Only one token exchange should proceed from here. Deduplication is performed second because in the case
                // of failure due to consent required, every caller needs to receive the 
                if (!await DeduplicatedTokenExchangeIdAsync(turnContext, cancellationToken).ConfigureAwait(false))
                {
                    // If the token is not exchangeable, do not process this activity further.
                    return;
                }
            }

            // Load OAuth flow state
            var stateKey = GetOAuthStorageKey(turnContext);
            var items = await _storage.ReadAsync([stateKey], cancellationToken);
            _flowState = items.TryGetValue(stateKey, out object value) ? (FlowState)value : new FlowState();

            await base.OnTurnAsync(turnContext, cancellationToken);

            // Store any changes to the OAuthFlow state after the turn is complete.
            items[stateKey] = _flowState;
            await _storage.WriteAsync(items, cancellationToken);
        }

        protected override Task OnTokenResponseEventAsync(ITurnContext<IEventActivity> turnContext, CancellationToken cancellationToken)
        {
            // TODO: what is this supposed to do?
            return Task.CompletedTask;
        }

        protected override async Task OnSignInInvokeAsync(ITurnContext<IInvokeActivity> turnContext, CancellationToken cancellationToken)
        {
            if (!_settings.IsOAuthEnabled)
            {
                await base.OnSignInInvokeAsync(turnContext, cancellationToken);
                return;
            }

            // Teams will send the bot an "Invoke" Activity that contains a value that will be exchanged for a token.
            await OnContinueFlow(turnContext, cancellationToken);
        }

        private async Task<TokenResponse> OnContinueFlow(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            TokenResponse tokenResponse = null;

            try
            {
                tokenResponse = await _flow.ContinueFlowAsync(turnContext, _flowState.FlowExpires, cancellationToken);
            }
            catch (TimeoutException)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text(_settings.TimeoutMessage), cancellationToken);
                if (_settings.AutoRetry)
                {
                    return await _flow.BeginFlowAsync(turnContext, null, cancellationToken);
                }
            }

            _flowState.FlowStarted = false;

            return tokenResponse;
        }

        private static bool ShouldExchange(ITurnContext turnContext)
        {
            // Teams
            if (string.Equals(Channels.Msteams, turnContext.Activity.ChannelId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(SignInConstants.TokenExchangeOperationName, turnContext.Activity.Name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // SharePoint
            if (string.Equals(Channels.M365, turnContext.Activity.ChannelId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(SignInConstants.SharePointTokenExchange, turnContext.Activity.Name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private async Task<bool> DeduplicatedTokenExchangeIdAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            // Create a StoreItem with Etag of the unique 'signin/tokenExchange' request
            var storeItem = new TokenStoreItem
            {
                ETag = ProtocolJsonSerializer.ToJsonElements(turnContext.Activity.Value)["id"].ToString(),
            };

            var storeItems = new Dictionary<string, object> { { TokenStoreItem.GetStorageKey(turnContext), storeItem } };
            try
            {
                // Writing the IStoreItem with ETag of unique id will succeed only once
                await _storage.WriteAsync(storeItems, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)

                // Memory storage throws a generic exception with a Message of 'Etag conflict. [other error info]'
                // CosmosDbPartitionedStorage throws: ex.Message.Contains("pre-condition is not met")
                when (ex.Message.StartsWith("Etag conflict", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("pre-condition is not met"))
            {
                // Do NOT proceed processing this message, some other thread or machine already has processed it.

                // Send 200 invoke response.
                await SendInvokeResponseAsync(turnContext, cancellationToken: cancellationToken).ConfigureAwait(false);
                return false;
            }

            return true;
        }

        private async Task SendInvokeResponseAsync(ITurnContext turnContext, object body = null, HttpStatusCode httpStatusCode = HttpStatusCode.OK, CancellationToken cancellationToken = default)
        {
            await turnContext.SendActivityAsync(
                new Activity
                {
                    Type = ActivityTypes.InvokeResponse,
                    Value = new InvokeResponse
                    {
                        Status = (int)httpStatusCode,
                        Body = body,
                    },
                }, cancellationToken).ConfigureAwait(false);
        }

        private async Task<bool> ExchangedTokenAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            TokenResponse tokenExchangeResponse = null;
            var tokenExchangeRequest = ProtocolJsonSerializer.ToObject<TokenExchangeInvokeRequest>(turnContext.Activity.Value);

            try
            {
                var userTokenClient = turnContext.TurnState.Get<IUserTokenClient>();
                if (userTokenClient != null)
                {
                    tokenExchangeResponse = await userTokenClient.ExchangeTokenAsync(
                        turnContext.Activity.From.Id,
                        _settings.ConnectionName,
                        turnContext.Activity.ChannelId,
                        new TokenExchangeRequest { Token = tokenExchangeRequest.Token },
                        cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    throw new NotSupportedException("Token Exchange is not supported by the current adapter.");
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types (ignoring, see comment below)
            catch
#pragma warning restore CA1031 // Do not catch general exception types
            {
                // Ignore Exceptions
                // If token exchange failed for any reason, tokenExchangeResponse above stays null,
                // and hence we send back a failure invoke response to the caller.
            }

            if (string.IsNullOrEmpty(tokenExchangeResponse?.Token))
            {
                // The token could not be exchanged (which could be due to a consent requirement)
                // Notify the sender that PreconditionFailed so they can respond accordingly.

                var invokeResponse = new TokenExchangeInvokeResponse
                {
                    Id = tokenExchangeRequest.Id,
                    ConnectionName = _settings.ConnectionName,
                    FailureDetail = "The bot is unable to exchange token. Proceed with regular login.",
                };

                await SendInvokeResponseAsync(turnContext, invokeResponse, HttpStatusCode.PreconditionFailed, cancellationToken).ConfigureAwait(false);

                return false;
            }

            return true;
        }

        private static string GetOAuthStorageKey(ITurnContext turnContext)
        {
            var channelId = turnContext.Activity.ChannelId ?? throw new InvalidOperationException("invalid activity-missing channelId");
            var conversationId = turnContext.Activity.Conversation?.Id ?? throw new InvalidOperationException("invalid activity-missing Conversation.Id");
            return $"{channelId}/conversations/{conversationId}/flowState";
        }

        private class TokenStoreItem : IStoreItem
        {
            public string ETag { get; set; }

            public static string GetStorageKey(ITurnContext turnContext)
            {
                var activity = turnContext.Activity;
                var channelId = activity.ChannelId ?? throw new InvalidOperationException("invalid activity-missing channelId");
                var conversationId = activity.Conversation?.Id ?? throw new InvalidOperationException("invalid activity-missing Conversation.Id");

                var value = activity.Value.ToJsonElements();
                if (value == null || !value.ContainsKey("id"))
                {
                    throw new InvalidOperationException("Invalid signin/tokenExchange. Missing activity.Value.Id.");
                }

                return $"{channelId}/{conversationId}/{value["id"]}";
            }
        }
    }

    class FlowState
    {
        public bool FlowStarted = false;
        public DateTime FlowExpires = DateTime.MinValue;
    }
}
