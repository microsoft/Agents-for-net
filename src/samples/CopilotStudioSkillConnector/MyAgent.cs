// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.CopilotStudio.Client;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace CopilotStudioSkillConnector
{
    public class MyAgent : AgentApplication
    {
        const string MCSConversationPropertyName = "MCSConversationId";
        private readonly ConnectionSettings _connectionSettings;
        private readonly IHttpClientFactory _httpClientFactory;

        public MyAgent(AgentApplicationOptions options, IHttpClientFactory httpClientFactory, IConfiguration config) : base(options)
        {
            _connectionSettings = new ConnectionSettings(config.GetSection("CopilotStudioAgent"));
            _httpClientFactory = httpClientFactory;

            //setup for non-exchangeable token in Azure Bot OAuth Connection
            OnMessage("-me", OnMeAsync, autoSignInHandlers: ["me"]);  

            //
            // The following is cumbersome when an SDK agent needs to handle non-MCS requests too. 
            //

            // Handle Messages from MCS with auth
            OnActivity(ActivityTypes.Message, OnMCSMessage, channelId: Channels.CopilotStudio, autoSignInHandlers: ["mcs"]);

            // Handle Messages from ABS with Token Service Auth
            OnActivity(ActivityTypes.Message, OnABSMessage, autoSignInHandlers: ["abs"]);
        }

        private async Task OnMCSMessage(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            await OnMessageAsync(turnContext, turnState, "mcs", cancellationToken);
        }

        private async Task OnABSMessage(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            await OnMessageAsync(turnContext, turnState, "abs", cancellationToken);
        }

        // Forward whatever the user said to MCS and reply with it's responses.
        private async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, string handler, CancellationToken cancellationToken)
        {
            var mcsConversationId = turnState.Conversation.GetValue<string>(MCSConversationPropertyName);
            var cpsClient = GetCopilotClient(turnContext, handler);

            if (string.IsNullOrEmpty(mcsConversationId))
            {
                // Regardless of the Activity  Type, start the conversation.
                await foreach (IActivity activity in cpsClient.StartConversationAsync(emitStartConversationEvent: true, cancellationToken: cancellationToken))
                {
                    if (activity.IsType(ActivityTypes.Message))
                    {
                        await turnContext.SendActivityAsync($"MCS Sez: {activity.Text}", cancellationToken: cancellationToken);

                        // Record the conversationId MCS is sending. It will be used this for subsequent messages.
                        turnState.Conversation.SetValue(MCSConversationPropertyName, activity.Conversation.Id);
                    }
                }
            }
            else if (turnContext.Activity.IsType(ActivityTypes.Message))
            {
                // Send the Copilot Studio Agent whatever the sent and send the responses back.
                await foreach (IActivity activity in cpsClient.AskQuestionAsync(turnContext.Activity.Text, mcsConversationId, cancellationToken))
                {
                    if (activity.IsType(ActivityTypes.Message))
                    {
                        await turnContext.SendActivityAsync($"MCS Sez: {activity.Text}", cancellationToken: cancellationToken);
                    }
                }
            }
        }

        private async Task OnMeAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            var name = await GetDisplayName(turnContext, "me");
            await turnContext.SendActivityAsync($"Hi, {name}", cancellationToken: cancellationToken);
        }

        private CopilotClient GetCopilotClient(ITurnContext turnContext, string handler)
        {
            return new CopilotClient(
                _connectionSettings,
                _httpClientFactory,
                tokenProviderFunction: async (s) =>
                {
                    return await UserAuthorization.ExchangeTurnTokenAsync(turnContext, handler, exchangeScopes: [CopilotClient.ScopeFromSettings(_connectionSettings)]);
                },
                NullLogger.Instance,
                "mcs");
        }

        /// <summary>
        /// Gets the display name of the user from the Graph API using the access token.
        /// </summary>
        private async Task<string> GetDisplayName(ITurnContext turnContext, string handleName)
        {
            string displayName = "Unknown";
            var graphInfo = await GetGraphInfo(turnContext, handleName);
            if (graphInfo != null)
            {
                displayName = graphInfo!["displayName"]!.GetValue<string>();
            }
            return displayName;
        }

        private async Task<JsonNode> GetGraphInfo(ITurnContext turnContext, string handleName)
        {
            string accessToken = await UserAuthorization.GetTurnTokenAsync(turnContext, handleName);
            string graphApiUrl = $"https://graph.microsoft.com/v1.0/me";
            try
            {
                using HttpClient client = new();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                HttpResponseMessage response = await client.GetAsync(graphApiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonNode.Parse(content)!;
                }
            }
            catch (Exception ex)
            {
                // Handle error response from Graph API
                System.Diagnostics.Trace.WriteLine($"Error getting display name: {ex.Message}");
            }
            return null!;
        }

    }
}
