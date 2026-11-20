// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.CopilotStudio.Client;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net.Http;
using System.Threading.Tasks;

namespace OBOAuthorization;

public static class OboAgentFactory
{
    public static AgentApplication Create(AgentApplicationOptions options, IHttpClientFactory clients, IConfiguration configuration)
    {
        const string MCSConversationPropertyName = "MCSConversationId";

        var app = new AgentApplication(options);

        CopilotClient GetClient(ITurnContext turnContext)
        {
            var settings = new ConnectionSettings(configuration.GetSection("CopilotStudioAgent"));
            string[] scopes = [CopilotClient.ScopeFromSettings(settings)];

            return new CopilotClient(
                settings,
                clients,
                tokenProviderFunction: async (s) =>
                {
                    // The OAuth connection returns an exchangeable token. Exchange it for the
                    // Copilot scopes here; other services can request different scopes in the same turn.
                    return await turnContext.ExchangeTurnTokenAsync("mcs", exchangeScopes: scopes);
                },
                NullLogger.Instance,
                "mcs");
        }

        app.OnMessage("-signout", async (turnContext, turnState, cancellationToken) =>
        {
            await app.UserAuthorization.SignOutUserAsync(turnContext, turnState, cancellationToken: cancellationToken);
            await turnContext.SendActivityAsync("You have signed out", cancellationToken: cancellationToken);
        }, rank: RouteRank.First);

        // The user token is available before these routes run.
        app.AddRoute(RouteBuilder.Create()
            .WithSelector((turnContext, cancellationToken) => Task.FromResult(true))
            .WithHandler(async (turnContext, turnState, cancellationToken) =>
            {
                var mcsConversationId = turnState.Conversation.GetValue<string>(MCSConversationPropertyName);
                var cpsClient = GetClient(turnContext);

                if (string.IsNullOrEmpty(mcsConversationId))
                {
                    // Any activity can start the conversation, including a conversation update.
                    await foreach (IActivity activity in cpsClient.StartConversationAsync(emitStartConversationEvent: true, cancellationToken: cancellationToken))
                    {
                        if (activity.IsType(ActivityTypes.Message))
                        {
                            await turnContext.SendActivityAsync(activity.Text, cancellationToken: cancellationToken);
                            turnState.Conversation.SetValue(MCSConversationPropertyName, activity.Conversation.Id);
                        }
                    }
                }
                else if (turnContext.Activity.IsType(ActivityTypes.Message))
                {
                    await foreach (IActivity activity in cpsClient.AskQuestionAsync(turnContext.Activity.Text, mcsConversationId, cancellationToken))
                    {
                        if (activity.IsType(ActivityTypes.Message))
                        {
                            await turnContext.SendActivityAsync(activity.Text, cancellationToken: cancellationToken);
                        }
                    }
                }
            })
            .Build());

        app.UserAuthorization.OnUserSignInFailure(async (turnContext, turnState, handlerName, response, initiatingActivity, cancellationToken) =>
        {
            await turnContext.SendActivityAsync($"SignIn failed with '{handlerName}': {response.Cause}/{response.Error!.Message}", cancellationToken: cancellationToken);
        });

        return app;
    }
}
