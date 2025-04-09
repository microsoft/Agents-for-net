// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
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
        /*
         During setup of the Agent Application, Register Event Handlers for the Agent. 
         For this example we will register a welcome message for the user when they join the conversation, then configure sign-in and sign-out commands.
         Additionally, we will add events to handle notifications of sign-in success and failure,  these notifications will report the local log instead of back to the calling agent. .

         This handler should only register events and setup state as it can be called multiple times before agent events are invoked. 
        */

        // this sets the default signin handler to the default handler name.  This is used to identify the handler that will be used for the sign-in process later in this code. 
        _signInHandlerName = Authorization.DefaultHandlerName;

        /*
         When a conversation update event is triggered. 
        */
        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);

        ///*
        // Handles the user sending a Login or LogOut command using the specific keywords '-signin' and '-signout'
        //*/
        // Handles the user sending a Login command using the specific keywords '-signin'
        OnMessage("-signin", SignInAsync);

        // Handles the user sending a Logout command using the specific keywords '-signout'
        OnMessage("-signout", async (turnContext, turnState, cancellationToken) =>
        {
            // force a user signout to reset the user state
            // this is needed to reset the token in Azure Bot Services if needed. 
            await Authorization.SignOutUserAsync(turnContext, turnState, cancellationToken: cancellationToken);
            await turnContext.SendActivityAsync("You have signed out", cancellationToken: cancellationToken);
        }, rank: RouteRank.Last);


        /*
         The Authorization Class provides methods and properties to manage and access user authentication tokens
         You can use this class to interact with the authentication process, including signing in and signing out users, accessing tokens, and handling authentication events.
         */

        // Register Events for SignIn and SignOut on the authentication class to track the status of the user, notify the user of the status of their authentication process, or log the status of the authentication process.
        Authorization.OnUserSignInSuccess(OnUserSignInSuccess);

        Authorization.OnUserSignInFailure(async (turnContext, turnState, handlerName, response, initiatingActivity, cancellationToken) =>
        {
            await turnContext.SendActivityAsync($"Manual Sign In: Failed to login to '{handlerName}': {response.Error.Message}", cancellationToken: cancellationToken);
        });

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
        /*
        In this example, we will send a welcome message to the user when they join the conversation.
        We do this by iterating over the incoming activity members added to the conversation and checking if the member is not the agent itself.
        Then a greeting notice is provided to each new member of the conversation.
        */
        foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"Welcome to the ManualSignIn Example.");
                sb.AppendLine("This Agent requires you to manually sign you in.");
                sb.AppendLine("You can use the following commands to interact with the agent:");
                sb.AppendLine("-signin: Sign into the agent.");
                sb.AppendLine("-signout: Sign out of the agent and force it to reset the login flow on next message.");
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
        /*
        IMPORTANT: The ReadMe associated with this sample, instructs you on configuring the Azure Bot Service Registration with the scopes to allow you to read your own information from Graph.  you must have completed that for this sample to work correctly. 

        In this scenario a specific login handler is called for,  if that logging handler succeeds the remainder of the turn process is handled in the OnUserSignInSuccess handler
        */
        await Authorization.SignInUserAsync(turnContext, turnState, _signInHandlerName, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Gets the display name of the user from the Graph API using the access token.
    /// </summary>
    /// <param name="turnContext"><see cref="ITurnState"/></param>
    /// <returns></returns>
    private async Task<string> GetDisplayName(ITurnContext turnContext)
    {
        string displayName = _defaultDisplayName;
        string accessToken = Authorization.GetTurnToken(_signInHandlerName);
        string graphApiUrl = $"https://graph.microsoft.com/v1.0/me";
        HttpClient client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        HttpResponseMessage response = await client.GetAsync(graphApiUrl);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            var graphResponse = JsonNode.Parse(content);
            displayName = graphResponse!["displayName"].GetValue<string>();
        }
        return displayName;
    }


    /// <summary>
    /// This method is called by the `OnMessage("-signin", SignInAsync);` registration to handle the sign-in process for the user. 
    /// This is a generic message handler pattern used by the agent SDK to allow a developer to attach specific implementation to a specific message.
    /// </summary>
    /// <param name="turnContext"><see cref="ITurnContext"/></param>
    /// <param name="turnState"><see cref="ITurnState"/></param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
    private async Task SignInAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        /*
        We are using the Authorization class to sign in the user to the authorization provider.
        This triggers the User login flow, and the user will be prompted to sign in using the authorization provider if needed.
        On Success, this will call the OnUserSignInSuccess event handler to notify the user of the success and provide the access token.  The token will also be available in the Authorization class.
         */
        await Authorization.SignInUserAsync(turnContext, turnState, _signInHandlerName, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Raised when the user successfully signs in to the authorization provider.
    /// This also provides the access token for the user.
    /// </summary>
    /// <param name="turnContext"><see cref="ITurnContext"/></param>
    /// <param name="turnState"><see cref="ITurnState"/></param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
    /// <param name="handlerName">this is the authorization handler that was used to execute the login.</param>
    /// <param name="token">This is the access token for the user</param>
    /// <param name="initiatingActivity">This is the activity that started the login flow, note that it may be different then the activity on the turnContext. </param>
    /// <returns></returns>
    private async Task OnUserSignInSuccess(ITurnContext turnContext, ITurnState turnState, string handlerName, string token, IActivity initiatingActivity, CancellationToken cancellationToken)
    {
        // The user has successfully signed in to the authorization provider and we will continue the turn. 

        // Handel the request for the designated login handler. 
        if (handlerName.Equals(_signInHandlerName))
        {
            // Check for Access Token from the Authorization Sub System. 
            if (string.IsNullOrEmpty(Authorization.GetTurnToken(handlerName)))
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
            await turnContext.SendActivityAsync($"**{displayName} said:** {initiatingActivity.Text}", cancellationToken: cancellationToken);
        }
        else
        {
            // Handle other login handlers here. 
            await turnContext.SendActivityAsync($"Manual Sign In: Successfully logged into '{handlerName}'", cancellationToken: cancellationToken);
        }

    }
}
