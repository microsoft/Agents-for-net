// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.CopilotStudio.Client;
using CopilotStudioClientSampleAPI.Models;

namespace CopilotStudioClientSampleAPI.Services;

/// <summary>
/// This class is responsible for handling the Chat Console service and managing the conversation between the user and the Copilot Studio hosted bot.
/// </summary>
/// <param name="copilotClient">Connection Settings for connecting to Copilot Studio</param>
internal class ChatConsoleService(CopilotClient copilotClient)
{
    const string _assistantRole = "assistant";
    const string _userRole = "user";
    const string _textType = "text";
    const string _markdownType = "markdown";

    public string response = string.Empty;
    /// <summary>
    /// This is the main thread loop that manages the back and forth communication with the Copilot Studio Bot. 
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task<Conversation> StartAsync(CancellationToken cancellationToken)
    {
        string responseText = string.Empty;
        string? conversationId = string.Empty;
        // Attempt to connect to the copilot studio hosted bot here
        // if successful, this will loop though all events that the Copilot Studio bot sends to the client setup the conversation. 
        await foreach (Activity act in copilotClient.StartConversationAsync(emitStartConversationEvent: true, cancellationToken: cancellationToken))
        {
            if (act is null)
            {
                throw new InvalidOperationException("Activity is null");
            }
            // for each response,  report to the UX
            responseText += PrintActivity(act);
            conversationId = act?.Conversation?.Id;
        }
        // Create the response object
        var response = new ChatResponse
        {
            Role = _assistantRole,
            Content =
                [
                    new Content { Type = "text", Text = responseText }
                ]
        };

        var conversation = new Conversation
        {
            ConversationId = conversationId!,
            ChatResponses = [response]
        };
        return conversation;
    }

    public async Task<List<ChatResponse>> Ask(string question, string conversationId, CancellationToken cancellationToken)
    {
        ChatResponse userResponse = CreateResponse(_userRole, question);

        var botResponse = new ChatResponse();

        // Get the response
        string responseText = string.Empty;
        // Send the user input to the Copilot Studio bot and await the response.
        // In this case we are not sending a conversation ID, as the bot is already connected by "StartConversationAsync", a conversation ID is persisted by the underlying client. 
        await foreach (Activity act in copilotClient.AskQuestionAsync(question, conversationId, cancellationToken))
        {
            // for each response,  report to the UX
            responseText += PrintActivity(act);
        }

        botResponse = CreateResponse(_assistantRole, responseText, _markdownType);
        return [userResponse, botResponse];
    }

    private static ChatResponse CreateResponse(string role, string content, string type = _textType)
    {
        // Create the response object
        return new ChatResponse
        {
            Role = role,
            Content =
                [
                    new Content { Type = type, Text = content }
                ]
        };
    }

    /// <summary>
    /// This method is responsible for writing formatted data to the console.
    /// This method does not handle all of the possible activity types and formats, it is focused on just a few common types. 
    /// </summary>
    /// <param name="act"></param>
    static string PrintActivity(Activity act)
    {
        string responseText = string.Empty;
        switch (act.Type)
        {
            case "message":
                if (act.TextFormat == _markdownType)
                {

                    responseText += "\n" + act.Text;
                    if (act.SuggestedActions?.Actions.Count > 0)
                    {
                        responseText += "Suggested actions:\n";
                        act.SuggestedActions.Actions.ToList().ForEach(action => responseText += "\t" + action.Text);
                    }
                }
                else
                {
                    responseText = $"\n{act.Text}\n";
                }
                break;
            case "typing":
                responseText += "";
                break;
            case "event":
                responseText += "";
                break;
            default:
                responseText += $"[{act.Type}]";
                break;
        }
        return responseText;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        System.Diagnostics.Trace.TraceInformation("Stopping");
        return Task.CompletedTask;
    }
}
