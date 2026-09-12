#nullable enable

using System.Threading;
using Microsoft.Agents.Core.Models;

internal sealed class ConversationSession
{
    private readonly ICopilotConversationClient _client;
    private readonly string _conversationId = $"Sample-{Guid.NewGuid():N}";
    private int _busy;

    internal ConversationSession(
        ICopilotConversationClient client,
        ActivityJournal journal,
        ActivityInterpreter interpreter)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        Journal = journal ?? throw new ArgumentNullException(nameof(journal));
        Interpreter = interpreter ?? throw new ArgumentNullException(nameof(interpreter));
    }

    internal ActivityJournal Journal { get; }

    internal ActivityInterpreter Interpreter { get; }

    public event EventHandler<PublishedActivityEventArgs>? ActivityPublished;

    public event EventHandler<bool>? BusyChanged;

    public event EventHandler<Exception>? Failed;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (Activity activity in _client.StartAsync(_conversationId, cancellationToken).WithCancellation(cancellationToken))
            {
                Publish(activity, ActivityDirection.Inbound);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Failed?.Invoke(this, exception);
            throw;
        }
    }

    public async Task SendAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
        {
            throw new InvalidOperationException("Conversation session is busy sending another activity.");
        }

        try
        {
            BusyChanged?.Invoke(this, true);

            Activity activity = MessageFactory.CreateMessageActivity(text) as Activity
                ?? throw new InvalidOperationException("MessageFactory did not create an Activity instance.");
            activity.Conversation = new ConversationAccount { Id = _conversationId };

            Publish(activity, ActivityDirection.Outbound);

            await foreach (Activity response in _client.ExecuteAsync(_conversationId, activity, cancellationToken).WithCancellation(cancellationToken))
            {
                Publish(response, ActivityDirection.Inbound);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Failed?.Invoke(this, exception);
            throw;
        }
        finally
        {
            if (Interlocked.Exchange(ref _busy, 0) != 0)
            {
                BusyChanged?.Invoke(this, false);
            }
        }
    }

    private void Publish(Activity activity, ActivityDirection direction)
    {
        ArgumentNullException.ThrowIfNull(activity);

        ActivityPublished?.Invoke(this, new PublishedActivityEventArgs(activity, direction));
    }
}

internal sealed class PublishedActivityEventArgs(Activity activity, ActivityDirection direction) : EventArgs
{
    public Activity Activity { get; } = activity ?? throw new ArgumentNullException(nameof(activity));

    public ActivityDirection Direction { get; } = direction;
}
