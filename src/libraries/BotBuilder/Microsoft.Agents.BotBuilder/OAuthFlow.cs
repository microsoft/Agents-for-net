// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Connector;
using Microsoft.Agents.Core.Interfaces;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.BotBuilder
{
    /// <summary>
    /// Creates a new prompt that asks the user to sign in using the UserTokenService Single Sign On (SSO)
    /// service.
    /// </summary>
    /// <remarks>
    /// The prompt will attempt to retrieve the users current token and if the user isn't signed in, it
    /// will send them an `OAuthCard|SigninCard` containing a button they can press to signin. Depending on the
    /// channel, the user will be sent through one of two possible signin flows:
    ///
    /// - The automatic signin flow where once the user signs in and the SSO service will forward the bot
    /// the users access token using either an `event` or `invoke` activity.
    /// - The "magic code" flow where once the user signs in they will be prompted by the SSO
    /// service to send the bot a six digit code confirming their identity. This code will be sent as a
    /// standard `message` activity.
    ///
    /// Both flows are automatically supported by the `OAuthFlow` and the only thing you need to be
    /// careful of is that you don't block the `event` and `invoke` activities that the prompt might
    /// be waiting on.
    /// <param name="title">The title of the OAuthCard|SigninCard sent.</param>
    /// <param name="text">The title of the OAuthCard|SigninCard sent.</param>
    /// <param name="connectionName">Required:  The name of the OAuth connection to use.</param>
    /// <param name="timeout">Option login timeout.</param>
    /// <param name="showSignInLink">Options, but true if the Channel requires it.</param>
    /// </remarks>
    /// <remarks>
    /// Initializes a new instance of the <see cref="OAuthFlow"/> class.
    /// </remarks>
    public class OAuthFlow(string title, string text, string connectionName, int? timeout, bool? showSignInLink)
    {
        public string Title { get; init; } = title ?? "Sign In";
        public string Text { get; init; } = text ?? "Please sign in";
        public string ConnectionName { get; init; } = connectionName ?? throw new ArgumentNullException(connectionName);
        public int? Timeout { get; init; } = timeout;
        public bool? ShowSignInLink { get; init; } = showSignInLink;

        public virtual async Task<TokenResponse> BeginFlowAsync(ITurnContext turnContext, IActivity? prompt, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(turnContext);

            // Attempt to get the users token
            var output = await GetTokenClient(turnContext).GetUserTokenAsync(turnContext.Activity.From.Id, ConnectionName, turnContext.Activity.ChannelId, magicCode: null, cancellationToken).ConfigureAwait(false);
            if (output != null)
            {
                // Return token
                return output;
            }

            // Prompt user to login
            await SendOAuthCardAsync(
                turnContext,
                prompt, cancellationToken).ConfigureAwait(false);

            return null;
        }

        /// <summary>
        /// Called when a prompt dialog is the active dialog and the user replied with a new activity.
        /// </summary>
        /// <param name="turnContext">The ITurnContext for the current turn of conversation.</param>
        /// <param name="expires">The DateTime the exchange expires.  Typically this would be stored after the BeginFlowAsync and used here.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="TokenResponse"/>The token response.</returns>
        /// <remarks>If successful, the result indicates whether the exchange is still
        /// active after the turn has been processed.
        /// <para>The prompt generally continues to receive the user's replies until it accepts the
        /// user's reply as valid input for the prompt.</para></remarks>
        /// <exception cref="TimeoutException"/>
        public virtual async Task<TokenResponse> ContinueFlowAsync(ITurnContext turnContext, DateTime expires, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(turnContext);

            // Check for timeout
            var hasTimedOut = HasTimedOut(turnContext, expires);
            if (hasTimedOut)
            {
                // if the token fetch request times out, complete the prompt with no result.
                throw new TimeoutException("OAuthFlow timeout");
            }

            // Recognize token
            return await RecognizeTokenAsync(turnContext, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Signs out the user.
        /// </summary>
        /// <param name="turnContext">Context for the current turn of conversation with the user.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        public async Task SignOutUserAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            await GetTokenClient(turnContext).SignOutUserAsync(turnContext.Activity.From.Id, ConnectionName, turnContext.Activity.ChannelId, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Returns the IUserTokenClient for the OAuthFlow.
        /// </summary>
        /// <param name="turnContext"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public static IUserTokenClient GetTokenClient(ITurnContext turnContext)
        {
            ArgumentNullException.ThrowIfNull(turnContext);
            var userTokenClient = turnContext.TurnState.Get<IUserTokenClient>();
            if (userTokenClient != null)
            {
                return userTokenClient;
            }
            throw new NotSupportedException("OAuthFlow: IUserTokenClient is not supported.  Was a IUserTokenClient registered?");
        }

        /// <summary>
        /// Provide subclasses a chance to alter the SignInCard or other signin Activity values.
        /// </summary>
        /// <param name="prompt"></param>
        /// <returns></returns>
        protected virtual IActivity AlterSignInPrompt(IActivity prompt)
        {
            return prompt;
        }

        /// <summary>
        /// Shared implementation of the SendOAuthCardAsync function. This is intended for internal use, to
        /// consolidate the implementation of the OAuthPrompt and OAuthInput. Application logic should use
        /// those dialog classes.
        /// </summary>
        /// <param name="settings">OAuthSettings.</param>
        /// <param name="turnContext">ITurnContext.</param>
        /// <param name="prompt">Activity.</param>
        /// <param name="cancellationToken">CancellationToken.</param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        private async Task SendOAuthCardAsync(ITurnContext turnContext, IActivity prompt, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(turnContext);

            // Ensure prompt initialized
            prompt ??= Activity.CreateMessageActivity();
            prompt.Attachments ??= [];

            // Append appropriate card if missing
            if (!ChannelSupportsOAuthCard(turnContext.Activity.ChannelId))
            {
                if (!prompt.Attachments.Any(a => a.Content is SigninCard))
                {
                    var signInResource = await GetTokenClient(turnContext).GetSignInResourceAsync(ConnectionName, turnContext.Activity, null, cancellationToken).ConfigureAwait(false);
                    prompt.Attachments.Add(new Attachment
                    {
                        ContentType = SigninCard.ContentType,
                        Content = new SigninCard
                        {
                            Text = Text,
                            Buttons =
                            [
                                new CardAction
                                {
                                    Title = Title,
                                    Value = signInResource.SignInLink,
                                    Type = ActionTypes.Signin,
                                },
                            ],
                        },
                    });
                }
            }
            else if (!prompt.Attachments.Any(a => a.Content is OAuthCard))
            {
                var cardActionType = ActionTypes.Signin;
                var signInResource = await GetTokenClient(turnContext).GetSignInResourceAsync(ConnectionName, turnContext.Activity, null, cancellationToken).ConfigureAwait(false);

                string value;
                if ((ShowSignInLink != null && ShowSignInLink == false) || 
                    (ShowSignInLink == null && !ChannelRequiresSignInLink(turnContext.Activity.ChannelId)))
                {
                    value = null;
                }
                else
                {
                    value = signInResource.SignInLink;
                }

                prompt.Attachments.Add(new Attachment
                {
                    ContentType = OAuthCard.ContentType,
                    Content = new OAuthCard
                    {
                        Text = Text,
                        ConnectionName = ConnectionName,
                        Buttons =
                        [
                            new CardAction
                            {
                                Title = Title,
                                Text = Text,
                                Type = cardActionType,
                                Value = value
                            },
                        ],
                        TokenExchangeResource = signInResource.TokenExchangeResource,
                        TokenPostResource = signInResource.TokenPostResource
                    },
                });
            }

            // Add the login timeout specified in OAuthPromptSettings to TurnState so it can be referenced if polling is needed
            if (!turnContext.TurnState.ContainsKey(OAuthTurnStateConstants.OAuthLoginTimeoutKey) && Timeout.HasValue)
            {
                turnContext.TurnState.Add<object>(OAuthTurnStateConstants.OAuthLoginTimeoutKey, TimeSpan.FromMilliseconds(Timeout.Value));
            }

            // Set input hint
            if (string.IsNullOrEmpty(prompt.InputHint))
            {
                prompt.InputHint = InputHints.AcceptingInput;
            }

            await turnContext.SendActivityAsync(AlterSignInPrompt(prompt), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Shared implementation of the RecognizeTokenAsync function. This is intended for internal use, to
        /// consolidate the implementation of the OAuthPrompt and OAuthInput. Application logic should use
        /// those dialog classes.
        /// </summary>
        /// <param name="settings">OAuthPromptSettings.</param>
        /// <param name="dc">DialogContext.</param>
        /// <param name="cancellationToken">CancellationToken.</param>
        /// <param name="turnContext"></param>
        /// <returns>PromptRecognizerResult.</returns>
        private async Task<TokenResponse> RecognizeTokenAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            TokenResponse result = null;
            if (IsTokenResponseEvent(turnContext))
            {
                result = ProtocolJsonSerializer.ToObject<TokenResponse>(turnContext.Activity.Value);
            }
            else if (IsVerificationInvoke(turnContext))
            {
                var value = ProtocolJsonSerializer.ToJsonElements(turnContext.Activity.Value);
                var magicCode = value.ContainsKey("state") ? ProtocolJsonSerializer.ToJsonElements(turnContext.Activity.Value)["state"].ToString() : null;

                // Getting the token follows a different flow in Teams. At the signin completion, Teams
                // will send the bot an "invoke" activity that contains a "magic" code. This code MUST
                // then be used to try fetching the token from UserToken service within some time
                // period. We try here. If it succeeds, we return 200 with an empty body. If it fails
                // with a retriable error, we return 500. Teams will re-send another invoke in this case.
                // If it fails with a non-retriable error, we return 404. Teams will not (still work in
                // progress) retry in that case.
                try
                {
                    result = await GetTokenClient(turnContext).GetUserTokenAsync(turnContext.Activity.From.Id, ConnectionName, turnContext.Activity.ChannelId, magicCode, cancellationToken).ConfigureAwait(false);

                    if (result != null)
                    {
                        await turnContext.SendActivityAsync(new Activity { Type = ActivityTypes.InvokeResponse }, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await SendInvokeResponseAsync(turnContext, HttpStatusCode.NotFound, null, cancellationToken).ConfigureAwait(false);
                    }
                }
#pragma warning disable CA1031 // Do not catch general exception types (ignoring exception for now and send internal server error, see comment above)
                catch
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    await SendInvokeResponseAsync(turnContext, HttpStatusCode.InternalServerError, null, cancellationToken).ConfigureAwait(false);
                }
            }
            else if (IsTokenExchangeRequestInvoke(turnContext))
            {
                var tokenExchangeRequest = turnContext.Activity.Value != null ? ProtocolJsonSerializer.ToObject<TokenExchangeInvokeRequest>(turnContext.Activity.Value) : null;

                if (tokenExchangeRequest == null)
                {
                    await SendInvokeResponseAsync(
                        turnContext,
                        HttpStatusCode.BadRequest,
                        new TokenExchangeInvokeResponse
                        {
                            Id = null,
                            ConnectionName = ConnectionName,
                            FailureDetail = "The bot received an InvokeActivity that is missing a TokenExchangeInvokeRequest value. This is required to be sent with the InvokeActivity.",
                        }, cancellationToken).ConfigureAwait(false);
                }
                else if (tokenExchangeRequest.ConnectionName != ConnectionName)
                {
                    await SendInvokeResponseAsync(
                        turnContext,
                        HttpStatusCode.BadRequest,
                        new TokenExchangeInvokeResponse
                        {
                            Id = tokenExchangeRequest.Id,
                            ConnectionName = ConnectionName,
                            FailureDetail = "The bot received an InvokeActivity with a TokenExchangeInvokeRequest containing a ConnectionName that does not match the ConnectionName expected by the bot's active OAuthPrompt. Ensure these names match when sending the InvokeActivityInvalid ConnectionName in the TokenExchangeInvokeRequest",
                        }, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    TokenResponse tokenExchangeResponse = null;
                    try
                    {
                        var userId = turnContext.Activity.From.Id;
                        var channelId = turnContext.Activity.ChannelId;
                        var exchangeRequest = new TokenExchangeRequest { Token = tokenExchangeRequest.Token };
                        tokenExchangeResponse = await GetTokenClient(turnContext).ExchangeTokenAsync(userId, ConnectionName, channelId, exchangeRequest, cancellationToken).ConfigureAwait(false);
                    }
#pragma warning disable CA1031 // Do not catch general exception types (ignoring, see comment below)
                    catch
#pragma warning restore CA1031 // Do not catch general exception types
                    {
                        // Ignore Exceptions
                        // If token exchange failed for any reason, tokenExchangeResponse above stays null , and hence we send back a failure invoke response to the caller.
                        // This ensures that the caller shows 
                    }

                    if (tokenExchangeResponse == null || string.IsNullOrEmpty(tokenExchangeResponse.Token))
                    {
                        await SendInvokeResponseAsync(
                            turnContext,
                            HttpStatusCode.PreconditionFailed,
                            new TokenExchangeInvokeResponse
                            {
                                Id = tokenExchangeRequest.Id,
                                ConnectionName = ConnectionName,
                                FailureDetail = "The bot is unable to exchange token. Proceed with regular login.",
                            }, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await SendInvokeResponseAsync(
                            turnContext,
                            HttpStatusCode.OK,
                            new TokenExchangeInvokeResponse
                            {
                                Id = tokenExchangeRequest.Id,
                                ConnectionName = ConnectionName,
                            }, cancellationToken).ConfigureAwait(false);

                        result = new TokenResponse
                        {
                            ChannelId = tokenExchangeResponse.ChannelId,
                            ConnectionName = tokenExchangeResponse.ConnectionName,
                            Token = tokenExchangeResponse.Token,
                        };
                    }
                }
            }
            else if (turnContext.Activity.Type == ActivityTypes.Message)
            {
                if (!string.IsNullOrEmpty(turnContext.Activity.Text))
                {
                    // regex to check if code supplied is a 6 digit numerical code (hence, a magic code).
                    var magicCodeRegex = new Regex(@"(\d{6})");
                    var matched = magicCodeRegex.Match(turnContext.Activity.Text);
                    if (matched.Success)
                    {
                        result = await GetTokenClient(turnContext).GetUserTokenAsync(
                            turnContext.Activity.From.Id, 
                            ConnectionName, 
                            turnContext.Activity.ChannelId, 
                            magicCode: matched.Value, 
                            cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            return result;
        }

        private static bool IsTokenResponseEvent(ITurnContext turnContext)
        {
            var activity = turnContext.Activity;
            return activity.Type == ActivityTypes.Event && activity.Name == SignInConstants.TokenResponseEventName;
        }

        private static bool IsVerificationInvoke(ITurnContext turnContext)
        {
            var activity = turnContext.Activity;
            return activity.Type == ActivityTypes.Invoke && activity.Name == SignInConstants.VerifyStateOperationName;
        }

        private static bool IsTokenExchangeRequestInvoke(ITurnContext turnContext)
        {
            var activity = turnContext.Activity;
            return activity.Type == ActivityTypes.Invoke && activity.Name == SignInConstants.TokenExchangeOperationName;
        }

        private static bool ChannelSupportsOAuthCard(string channelId)
        {
            return channelId switch
            {
                Channels.Cortana or Channels.Skype or Channels.Skypeforbusiness => false,
                _ => true,
            };
        }

        private static bool ChannelRequiresSignInLink(string channelId)
        {
            return channelId switch
            {
                Channels.Msteams => true,
                _ => false,
            };
        }


        private static bool HasTimedOut(ITurnContext context, DateTime expires)
        {
            var isMessage = context.Activity.Type == ActivityTypes.Message;

            // If the incoming Activity is a message, or an Activity Type normally handled by OAuthPrompt,
            // check to see if this OAuthPrompt Expiration has elapsed, and end the dialog if so.
            var isTimeoutActivityType = isMessage
                            || IsTokenResponseEvent(context)
                            || IsVerificationInvoke(context)
                            || IsTokenExchangeRequestInvoke(context);
            return isTimeoutActivityType && DateTime.Compare(DateTime.UtcNow, expires) > 0;
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
    }
}
