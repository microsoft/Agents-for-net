#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Core.Models;

internal sealed class FakeCopilotConversationClient : ICopilotConversationClient
{
    public IReadOnlyList<Activity> StartActivities { get; init; } = [];

    public IReadOnlyList<Activity> ExecuteActivities { get; init; } = [];

    public List<Activity> Requests { get; } = [];

    public Exception? StartException { get; set; }

    public Exception? ExecuteException { get; set; }

    public TaskCompletionSource<object?>? ExecuteStarted { get; set; }

    public TaskCompletionSource<object?>? ReleaseExecute { get; set; }

    public async IAsyncEnumerable<Activity> StartAsync(
        string conversationId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (StartException is not null)
        {
            throw StartException;
        }

        foreach (Activity activity in StartActivities)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return activity;
            await Task.Yield();
        }
    }

    public async IAsyncEnumerable<Activity> ExecuteAsync(
        string conversationId,
        Activity activity,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Requests.Add(activity);
        ExecuteStarted?.TrySetResult(null);

        if (ReleaseExecute is not null)
        {
            await ReleaseExecute.Task.WaitAsync(cancellationToken);
        }

        if (ExecuteException is not null)
        {
            throw ExecuteException;
        }

        foreach (Activity response in ExecuteActivities)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return response;
            await Task.Yield();
        }
    }
}
