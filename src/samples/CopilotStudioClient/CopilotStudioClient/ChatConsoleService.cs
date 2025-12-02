// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.CopilotStudio.Client;
using Microsoft.Agents.Core.Models.Activities;

namespace CopilotStudioClientSample;

/// <summary>
/// This class is responsible for handling the Chat Console service and managing the conversation between the user and the Copilot Studio hosted Agent.
/// </summary>
/// <param name="copilotClient">Connection Settings for connecting to Copilot Studio</param>
internal class ChatConsoleService(CopilotClient copilotClient) : IHostedService
{
    /// <summary>
    /// This is the main thread loop that manages the back and forth communication with the Copilot Studio Agent. 
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="System.InvalidOperationException"></exception>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
        Console.Write("\nagent> ");
        // Attempt to connect to the copilot studio hosted agent here
        // if successful, this will loop though all events that the Copilot Studio agent sends to the client setup the conversation. 
        await foreach (IActivity act in copilotClient.StartConversationAsync(emitStartConversationEvent: true, cancellationToken: cancellationToken))
        {
            System.Diagnostics.Trace.WriteLine($">>>>MessageLoop Duration: {sw.Elapsed.ToDurationString()}");
            sw.Restart();
            if (act is null)
            {
                throw new InvalidOperationException("Activity is null");
            }
            // for each response,  report to the UX
            PrintActivity(act);
        }

        // Once we are connected and have initiated the conversation,  begin the message loop with the Console. 
        while (!cancellationToken.IsCancellationRequested)
        {
            Console.Write("\nuser> ");
            string question = Console.ReadLine()!; // Get user input from the console to send. 
            Console.Write("\nagent> ");
            // Send the user input to the Copilot Studio agent and await the response.
            // In this case we are not sending a conversation ID, as the agent is already connected by "StartConversationAsync", a conversation ID is persisted by the underlying client. 
            sw.Restart();
            await foreach (Activity act in copilotClient.AskQuestionAsync(question, null, cancellationToken))
            {
                System.Diagnostics.Trace.WriteLine($">>>>MessageLoop Duration: {sw.Elapsed.ToDurationString()}");
                // for each response,  report to the UX
                PrintActivity(act);
                sw.Restart();
            }
        }
        sw.Stop();
    }

    /// <summary>
    /// This method is responsible for writing formatted data to the console.
    /// This method does not handle all of the possible activity types and formats, it is focused on just a few common types. 
    /// </summary>
    /// <param name="act"></param>
    static void PrintActivity(IActivity act)
    {
        if (act is IMessageActivity message)
        {
            if (message.TextFormat == "markdown")
            {

                Console.WriteLine(message.Text);
                if (message.SuggestedActions?.Actions.Count > 0)
                {
                    Console.WriteLine("Suggested actions:\n");
                    message.SuggestedActions.Actions.ToList().ForEach(action => Console.WriteLine("\t" + action.Text));
                }
            }
            else
            {
                Console.Write($"\n{message.Text}\n");
            }
        }
        else if (act is ITypingActivity)
        {
            Console.Write(".");
        }
        else if (act is IEventActivity)
        {
            Console.Write("+");
        }
        else 
        {
            Console.Write($"[{act.Type}]");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        System.Diagnostics.Trace.TraceInformation("Stopping");
        return Task.CompletedTask;
    }
}
