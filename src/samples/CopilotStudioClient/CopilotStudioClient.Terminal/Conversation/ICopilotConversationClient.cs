#nullable enable

using System.Collections.Generic;
using System.Threading;
using Microsoft.Agents.Core.Models;

internal interface ICopilotConversationClient
{
    IAsyncEnumerable<Activity> StartAsync(string conversationId, CancellationToken cancellationToken);

    IAsyncEnumerable<Activity> ExecuteAsync(
        string conversationId,
        Activity activity,
        CancellationToken cancellationToken);
}
