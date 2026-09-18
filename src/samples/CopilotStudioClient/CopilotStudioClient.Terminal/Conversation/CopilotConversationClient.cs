#nullable enable

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Agents.CopilotStudio.Client;
using Microsoft.Agents.CopilotStudio.Client.Models;
using Microsoft.Agents.Core.Models;

internal sealed class CopilotConversationClient(CopilotClient client)
    : ICopilotConversationClient
{
    private readonly CopilotClient _client = client ?? throw new ArgumentNullException(nameof(client));

    public IAsyncEnumerable<Activity> StartAsync(
        string conversationId,
        CancellationToken cancellationToken)
    {
        return EnumerateActivitiesAsync(
            _client.StartConversationAsync(
                new StartRequest
                {
                    EmitStartConversationEvent = true,
                    ConversationId = conversationId
                },
                cancellationToken),
            cancellationToken);
    }

    public IAsyncEnumerable<Activity> ExecuteAsync(
        string conversationId,
        Activity activity,
        CancellationToken cancellationToken)
    {
        return EnumerateActivitiesAsync(
            _client.ExecuteAsync(conversationId, activity, cancellationToken),
            cancellationToken);
    }

    private static async IAsyncEnumerable<Activity> EnumerateActivitiesAsync(
        IAsyncEnumerable<IActivity> source,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (IActivity activity in source.WithCancellation(cancellationToken))
        {
            yield return activity as Activity
                ?? throw new InvalidOperationException("Copilot transport returned a non-Activity implementation.");
        }
    }
}
