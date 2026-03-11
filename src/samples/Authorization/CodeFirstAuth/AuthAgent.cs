// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.UserAuth;
using Microsoft.Agents.Core.Models;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace CodeFirstAuth;

public class AuthAgent : AgentApplication
{
    /// <summary>
    /// Describes the agent registration for the Authorization Agent
    /// This agent will handle the sign-in and sign-out OAuth processes for a user.
    /// </summary>
    /// <param name="options">AgentApplication Configuration objects to configure and setup the Agent Application</param>
    public AuthAgent(AgentApplicationOptions options) : base(options)
    {
        // During setup of the Agent Application, Register Event Handlers for the Agent. 
        // For this example we will register a welcome message for the user when they join the conversation, then configure sign-in and sign-out commands.
        // Additionally, we will add events to handle notifications of sign-in success and failure,  these notifications will report the local log instead of back to the calling agent.

        // When a conversation update event is triggered. 
        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);

        // Demonstrates the use of Per-Route Auto sign-in.  This will automatically get a token using the indicated OAuth handler for this message route.
        // This Route will automatically get a token using the "me" UserAuthorization.Handler in config.
        OnMessage("-me", OnMe, autoSignInHandlers: ["me"]);

        // Handles the user sending a SignOut command using the specific keywords '-signout'
        OnMessage("-signout", async (turnContext, turnState, cancellationToken) =>
        {
            // Force a user signout to reset the user state
            // This is needed to reset the token in Azure Bot Services if needed. 
            // Typically this wouldn't be need in a production Agent.  Made available to assist it starting from scratch.
            await UserAuthorization.SignOutUserAsync(turnContext, turnState, "me", cancellationToken: cancellationToken);
            await turnContext.SendActivityAsync("You have signed out", cancellationToken: cancellationToken);
        }, rank: RouteRank.Last);

        // Registers a general event handler that will pick up any message activity that is not covered by the previous events handlers. 
        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);

        // The UserAuthorization Class provides methods and properties to manage and access user authorization tokens
        // You can use this class to interact with the UserAuthorization process, including signing in and signing out users, accessing tokens, and handling authorization events.

        // Register handler on the UserAuthorization class to notify the Agent in the event of an OAuth failure.
        // For a production Agent, this would typically be used to provide instructions to the end-user.  For example, call/email or
        // handoff to a live person (depending on Agent capabilities).
        UserAuthorization.OnUserSignInFailure(OnUserSignInFailure);
    }

    /// <summary>
    /// This method is called to handle the conversation update event when a new member is added to or removed from the conversation.
    /// </summary>
    /// <param name="turnContext"><see cref="ITurnContext"/></param>
    /// <param name="turnState"><see cref="ITurnState"/></param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
    private async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        // In this example, we will send a welcome message to the user when they join the conversation.
        // We do this by iterating over the incoming activity members added to the conversation and checking if the member is not the agent itself.
        // Then a greeting notice is provided to each new member of the conversation.
        
        foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                StringBuilder sb = new();
                sb.AppendLine("You can use the following commands to interact with the agent:");
                sb.AppendLine("**-me**: Displays detailed information using an OAuth Token.");
                sb.AppendLine("**-signout**: Sign out of the agent and force it to reset the login flow on next message.");
                sb.AppendLine("");
                sb.AppendLine("Type anything else to see the agent echo back your message.");
                await turnContext.SendActivityAsync(MessageFactory.Text(sb.ToString()), cancellationToken);
                sb.Clear();
            }
        }
    }

    /// Handles -me, using a different OAuthConnection to show Per-Route OAuth. 
    /// </summary>
    /// <param name="turnContext"><see cref="ITurnContext"/></param>
    /// <param name="turnState"><see cref="ITurnState"/></param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
    private async Task OnMe(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        // If successful, the user will be the token will be available via UserAuthorization.GetTurnTokenAsync(turnContext, "me"). 
        // If not successful, this handler won't be reached.  Instead, OnUserSignInFailure handler would have been called. 

        var graphInfo = await GetGraphInfo(turnContext, "me");

        var meInfo = $"Name: {graphInfo["displayName"]?.GetValue<string>()}{Environment.NewLine}Job Title: {graphInfo["jobTitle"]?.GetValue<string>()}{Environment.NewLine}Email: {graphInfo["mail"]?.GetValue<string>()}";
        await turnContext.SendActivityAsync(meInfo, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Handles general message loop. 
    /// </summary>
    /// <param name="turnContext"><see cref="ITurnContext"/></param>
    /// <param name="turnState"><see cref="ITurnState"/></param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
    /// <returns></returns>
    private async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        // Echo back what was said with your display name. 
        await turnContext.SendActivityAsync($"**You said:** {turnContext.Activity.Text}", cancellationToken: cancellationToken);
    }

    /// <summary>
    /// This method is called when the sign-in process fails with an error indicating why . 
    /// </summary>
    /// <param name="turnContext"></param>
    /// <param name="turnState"></param>
    /// <param name="handlerName"></param>
    /// <param name="response"></param>
    /// <param name="initiatingActivity"></param>
    /// <param name="cancellationToken"></param>
    private async Task OnUserSignInFailure(ITurnContext turnContext, ITurnState turnState, string handlerName, SignInResponse response, IActivity initiatingActivity, CancellationToken cancellationToken)
    {
        // Raise a notification to the user that the sign-in process failed.  In a production Agent, this would be used
        // to display alternative ways to get help, or in some cases transfer to a live agent.
        await turnContext.SendActivityAsync($"Sign In: Failed to login to '{handlerName}': {response.Cause}/{response.Error!.Message}", cancellationToken: cancellationToken);
    }

    private static async Task<JsonNode> GetGraphInfo(ITurnContext turnContext, string? handlerName = null)
    {
        // In this sample, a null handlerName will always return the "auto" token.
        string accessToken = await turnContext.GetTurnTokenAsync(handlerName!);
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
