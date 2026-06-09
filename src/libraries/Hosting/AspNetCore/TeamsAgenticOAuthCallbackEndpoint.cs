// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Authentication.Msal;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.UserAuth.TeamsAgentic;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore
{
    /// <summary>
    /// Handles the OAuth callback from Azure AD for Teams SSO authorization code exchange.
    /// After exchanging the code for a token, sends a proactive signin/verifyState invoke
    /// back to the conversation so the auth flow can complete.
    /// </summary>
    internal static class TeamsAgenticOAuthCallbackEndpoint
    {
        private const string SuccessHtml = @"<!DOCTYPE html>
<html>
<head><title>Sign-in Complete</title></head>
<body>
<h2>Sign-in successful!</h2>
<p>You can close this window and return to the conversation.</p>
<script>window.close();</script>
</body>
</html>";

        private const string ErrorHtml = @"<!DOCTYPE html>
<html>
<head><title>Sign-in Failed</title></head>
<body>
<h2>Sign-in failed</h2>
<p>{0}</p>
</body>
</html>";

        public static async Task HandleCallbackAsync(HttpContext httpContext)
        {
            var logger = httpContext.RequestServices.GetService<ILogger<IAgentHttpAdapter>>();
            var storage = httpContext.RequestServices.GetRequiredService<IStorage>();
            var connections = httpContext.RequestServices.GetRequiredService<IConnections>();
            var adapter = httpContext.RequestServices.GetRequiredService<IChannelAdapter>();
            var agent = httpContext.RequestServices.GetRequiredService<IAgent>();
            var cancellationToken = httpContext.RequestAborted;

            var code = httpContext.Request.Query["code"].ToString();
            var state = httpContext.Request.Query["state"].ToString();
            var error = httpContext.Request.Query["error"].ToString();
            var errorDescription = httpContext.Request.Query["error_description"].ToString();

            if (!string.IsNullOrEmpty(error))
            {
                logger?.LogWarning("OAuth callback received error: {Error} - {Description}", error, errorDescription);
                httpContext.Response.ContentType = "text/html";
                await httpContext.Response.WriteAsync(
                    string.Format(ErrorHtml, System.Net.WebUtility.HtmlEncode($"{error}: {errorDescription}")),
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            {
                httpContext.Response.ContentType = "text/html";
                httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                await httpContext.Response.WriteAsync(
                    string.Format(ErrorHtml, "Missing authorization code or state parameter."),
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            var storageKey = $"teamsagentic/pending/{state}";
            var items = await storage.ReadAsync([storageKey], cancellationToken).ConfigureAwait(false);
            if (!items.TryGetValue(storageKey, out var stateObj) || stateObj is not OAuthCallbackState pendingState)
            {
                httpContext.Response.ContentType = "text/html";
                httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                await httpContext.Response.WriteAsync(
                    string.Format(ErrorHtml, "Invalid or expired state. Please try signing in again."),
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            if (DateTime.UtcNow > pendingState.Expires)
            {
                await storage.DeleteAsync([storageKey], cancellationToken).ConfigureAwait(false);
                httpContext.Response.ContentType = "text/html";
                httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                await httpContext.Response.WriteAsync(
                    string.Format(ErrorHtml, "Sign-in request has expired. Please try again."),
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            try
            {
                var connection = connections.GetConnection(pendingState.ConnectionName);
                if (connection is not IMSALProvider msalProvider)
                {
                    throw new InvalidOperationException($"Connection '{pendingState.ConnectionName}' does not support MSAL.");
                }

                var msalApp = msalProvider.GetOrCreateConfidentialClient(pendingState.RedirectUri);

                var result = await msalApp
                    .AcquireTokenByAuthorizationCode(pendingState.Scopes, code)
                    .WithPkceCodeVerifier(pendingState.CodeVerifier)
                    .ExecuteAsync(cancellationToken)
                    .ConfigureAwait(false);

                // Clean up pending state
                await storage.DeleteAsync([storageKey], cancellationToken).ConfigureAwait(false);

                logger?.LogInformation("OAuth callback: successfully exchanged code for token. HomeAccountId={HomeAccountId}", pendingState.HomeAccountId);

                // Process a signin/verifyState invoke locally through the bot's pipeline.
                // This does NOT send the activity to the channel — it runs the agent's handlers directly.
                var convRef = pendingState.ConversationReference;
                var invokeActivity = new Activity
                {
                    Type = ActivityTypes.Invoke,
                    Name = SignInConstants.VerifyStateOperationName,
                    Value = new Dictionary<string, string> { ["token"] = result.AccessToken },
                    ChannelId = convRef.ChannelId,
                    ServiceUrl = convRef.ServiceUrl,
                    Conversation = convRef.Conversation,
                    From = convRef.User,
                    Recipient = convRef.Agent
                };

                var identity = AgentClaims.CreateIdentity(pendingState.BotClientId);
                await adapter.ProcessActivityAsync(identity, (IActivity)invokeActivity, agent.OnTurnAsync, cancellationToken).ConfigureAwait(false);

                httpContext.Response.ContentType = "text/html";
                await httpContext.Response.WriteAsync(SuccessHtml, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "OAuth callback: failed to exchange authorization code.");

                // Clean up pending state and notify the agent's pipeline of the failure.
                await storage.DeleteAsync([storageKey], cancellationToken).ConfigureAwait(false);

                var convRef = pendingState.ConversationReference;
                var failureActivity = new Activity
                {
                    Type = ActivityTypes.Invoke,
                    Name = SignInConstants.SignInFailure,
                    Value = new Dictionary<string, string> { ["error"] = ex.Message },
                    ChannelId = convRef.ChannelId,
                    ServiceUrl = convRef.ServiceUrl,
                    Conversation = convRef.Conversation,
                    From = convRef.User,
                    Recipient = convRef.Agent
                };

                var identity = AgentClaims.CreateIdentity(pendingState.BotClientId);
                await adapter.ProcessActivityAsync(identity, (IActivity)failureActivity, agent.OnTurnAsync, cancellationToken).ConfigureAwait(false);

                httpContext.Response.ContentType = "text/html";
                httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await httpContext.Response.WriteAsync(
                    string.Format(ErrorHtml, "An error occurred during sign-in. Please try again."),
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
