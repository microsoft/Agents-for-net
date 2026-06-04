// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.UserAuth;
using Microsoft.Agents.Core.Models;
using System.Threading;
using System.Threading.Tasks;

namespace SidecarAuth;

public class SidecarAgent : AgentApplication
{
    /// <summary>
    /// Default Sign In Name
    /// </summary>
    private readonly string _defaultDisplayName = "Unknown User";

    /// <summary>
    /// Describes the agent registration for the Authorization Agent
    /// This agent will handle the sign-in and sign-out OAuth processes for a user.
    /// </summary>
    /// <param name="options">AgentApplication Configuration objects to configure and setup the Agent Application</param>
    public SidecarAgent(AgentApplicationOptions options) : base(options)
    {
        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);

        OnMessage("-signout", async (turnContext, turnState, cancellationToken) =>
        {
            await UserAuthorization.SignOutUserAsync(turnContext, turnState, "me", cancellationToken: cancellationToken);
            await turnContext.SendActivityAsync("You have signed out", cancellationToken: cancellationToken);
        }, rank: RouteRank.Last);

        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);

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
                await turnContext.SendActivityAsync("Welcome!", cancellationToken: cancellationToken);
            }
        }
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
}
