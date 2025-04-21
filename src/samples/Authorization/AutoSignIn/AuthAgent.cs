// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
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


public class AuthAgent : AgentApplication
{
    /// <summary>
    /// Default Sign In Name
    /// </summary>
    private string _defaultDisplayName = "Unknown User";
    /// <summary>
    /// Authorization Handler Name to use for queries 
    /// </summary>
    private string _signInHandlerName = string.Empty;

    /// <summary>
    /// Describes the agent registration for the Authorization Agent
    /// This agent will handle the sign-in and sign-out processes for a user.
    /// </summary>
    /// <param name="options">AgentApplication Configuration objects to configure and setup the Agent Application</param>
    public AuthAgent(AgentApplicationOptions options) : base(options)
    {
        
         //During setup of the Agent Application, Register Event Handlers for the Agent. 
         //For this example we will register a welcome message for the user when they join the conversation, then configure sign-in and sign-out commands.
         //Additionally, we will add events to handle notifications of sign-in success and failure,  these notifications will report the local log instead of back to the calling agent. .

         //This handler should only register events and setup state as it can be called multiple times before agent events are invoked. 
        

        // this sets the default signin handler to the default handler name.  This is used to identify the handler that will be used for the sign-in process later in this code. 
        _signInHandlerName = UserAuthorization.DefaultHandlerName;

        // When a conversation update event is triggered. 
        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);

        
        // Handles the user sending a Login or LogOut command using the specific keywords '-signout'
        OnMessage("-signout", async (turnContext, turnState, cancellationToken) =>
        {
            // force a user signout to reset the user state
            // this is needed to reset the token in Azure Bot Services if needed. 
            await UserAuthorization.SignOutUserAsync(turnContext, turnState, cancellationToken: cancellationToken);
            await turnContext.SendActivityAsync("You have signed out", cancellationToken: cancellationToken);
        }, rank: RouteRank.Last);


        
         //The UserAuthorization Class provides methods and properties to manage and access user authentication tokens
         //You can use this class to interact with the authentication process, including signing in and signing out users, accessing tokens, and handling authentication events.

        // Register Events for SignIn on the authentication class to track the status of the user, notify the user of the status of their authentication process, or log the status of the authentication process.
        UserAuthorization.OnUserSignInFailure(OnUserSignInFailure);

        // Registers a general event handler that will pick up any message activity that is not covered by the previous events handlers. 
        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);
    }

    /// <summary>
    /// This method is called to handle the conversation update event when a new member is added to or removed from the conversation.
    /// </summary>
    /// <param name="turnContext"><see cref="ITurnContext"/></param>
    /// <param name="turnState"><see cref="ITurnState"/></param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
    private async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        
        //In this example, we will send a welcome message to the user when they join the conversation.
        //We do this by iterating over the incoming activity members added to the conversation and checking if the member is not the agent itself.
        //Then a greeting notice is provided to each new member of the conversation.
        
        foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                string displayName = await GetDisplayName(turnContext);
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"Welcome to the AutoSignIn Example, **{displayName}**!.");
                sb.AppendLine("This Agent automatically signs you in when you first connect.");
                sb.AppendLine("You can use the following commands to interact with the agent:");
                sb.AppendLine("-signout: Sign out of the agent and force it to reset the login flow on next message.");
                if (displayName.Equals(_defaultDisplayName))
                {
                    sb.AppendLine("**WARNING: We were unable to get your display name with the current access token.. please use the -signout command before proceeding**");
                }
                sb.AppendLine("");
                sb.AppendLine("Type anything else to see the agent echo back your message.");
                await turnContext.SendActivityAsync(MessageFactory.Text(sb.ToString()), cancellationToken);
                sb.Clear();
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
        
        //When Auto Sign in is properly configured, the user will be automatically signed in when they first connect to the agent using the default handler chosen in the UserAuthorization configuration.
        //IMPORTANT: The ReadMe associated with this sample, instructs you on configuring the Azure Bot Service Registration with the scopes to allow you to read your own information from Graph.  you must have completed that for this sample to work correctly. 

        //If the sign in process is successful, the user will be signed in and the token will be available from the UserAuthorization.GetTurnToken(_signInHandlerName) function call. 
        //if the sign in was not successful,  you will get a Null when you call the UserAuthorization.GetTurnToken(_signInHandlerName) function. 
        

        // Check for Access Token from the UserAuthorization Sub System. 
        if (string.IsNullOrEmpty(UserAuthorization.GetTurnToken(_signInHandlerName)))
        {
            // Failed to get access token here, and we will now bail out of this message loop. 
            await turnContext.SendActivityAsync($"The auto sign in process failed and no access token is available", cancellationToken: cancellationToken);
            return;
        }

        // We have the access token, now try to get your user name from graph. 
        string displayName = await GetDisplayName(turnContext);
        if (displayName.Equals(_defaultDisplayName))
        {
            // Handle error response from Graph API
            await turnContext.SendActivityAsync($"Failed to get user information from Graph API \nDid you update the scope correctly in Azure bot Service?. If so type in -signout to force signout the current user", cancellationToken: cancellationToken);
            return;
        }

        // Now Echo back what was said with your display name. 
        await turnContext.SendActivityAsync($"**{displayName} said:** {turnContext.Activity.Text}", cancellationToken: cancellationToken);
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
    /// <returns></returns>
    private async Task OnUserSignInFailure(ITurnContext turnContext, ITurnState turnState, string handlerName, SignInResponse response, IActivity initiatingActivity, CancellationToken cancellationToken)
    {
        // Raise a notification to the user that the sign-in process failed.
        await turnContext.SendActivityAsync($"Manual Sign In: Failed to login to '{handlerName}': {response.Error.Message}", cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Gets the display name of the user from the Graph API using the access token.
    /// </summary>
    /// <param name="turnContext"><see cref="ITurnState"/></param>
    /// <returns></returns>
    private async Task<string> GetDisplayName(ITurnContext turnContext)
    {
        string displayName = _defaultDisplayName;
        string accessToken = UserAuthorization.GetTurnToken(_signInHandlerName);
        string graphApiUrl = $"https://graph.microsoft.com/v1.0/me";
        try
        {
            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            HttpResponseMessage response = await client.GetAsync(graphApiUrl);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var graphResponse = JsonNode.Parse(content);
                displayName = graphResponse!["displayName"].GetValue<string>();
            }
        }
        catch (Exception ex)
        {
            // Handle error response from Graph API
            System.Diagnostics.Trace.WriteLine($"Error getting display name: {ex.Message}");
        }
        return displayName;
    }


}
