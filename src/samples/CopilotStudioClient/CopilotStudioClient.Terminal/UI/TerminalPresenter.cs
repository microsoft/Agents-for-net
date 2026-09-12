#nullable enable

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Core.Models;

internal sealed class TerminalPresenter : IDisposable
{
    private readonly ConversationSession _session;
    private readonly ActivityJournal _journal;
    private readonly ActivityInterpreter _interpreter;
    private readonly ITerminalView _view;
    private bool _disposed;

    internal TerminalPresenter(
        ConversationSession session,
        ActivityJournal journal,
        ActivityInterpreter interpreter,
        ITerminalView view)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _journal = journal ?? throw new ArgumentNullException(nameof(journal));
        _interpreter = interpreter ?? throw new ArgumentNullException(nameof(interpreter));
        _view = view ?? throw new ArgumentNullException(nameof(view));

        _session.ActivityPublished += OnActivityPublished;
        _session.BusyChanged += OnBusyChanged;
        _session.Failed += OnSessionFailed;
        _journal.RecordAdded += OnRecordAdded;
    }

    internal Task StartAsync(CancellationToken cancellationToken)
    {
        return _session.StartAsync(cancellationToken);
    }

    internal async Task SendAsync(string? text, CancellationToken cancellationToken)
    {
        string trimmed = text?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            _view.SetStatus("Enter a message to send.", DiagnosticSeverity.Information);
            return;
        }

        await _session.SendAsync(trimmed, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _session.ActivityPublished -= OnActivityPublished;
        _session.BusyChanged -= OnBusyChanged;
        _session.Failed -= OnSessionFailed;
        _journal.RecordAdded -= OnRecordAdded;
        _disposed = true;
    }

    private void OnActivityPublished(object? sender, PublishedActivityEventArgs args)
    {
        _journal.Append(args.Activity, args.Direction);
        IReadOnlyList<ChatChange> changes = _interpreter.Process(args.Activity, args.Direction);
        _view.ApplyChatChanges(changes);
    }

    private void OnBusyChanged(object? sender, bool isBusy)
    {
        _view.SetBusy(isBusy);
    }

    private void OnRecordAdded(object? sender, ActivityRecord record)
    {
        _view.AddActivity(record);
    }

    private void OnSessionFailed(object? sender, Exception exception)
    {
        string message = GetStatusMessage(exception);
        _journal.AppendDiagnostic(message, DiagnosticSeverity.Error);
        _view.SetStatus(message, DiagnosticSeverity.Error);
    }

    private static string GetStatusMessage(Exception exception)
    {
        return string.IsNullOrWhiteSpace(exception.Message)
            ? exception.GetType().Name
            : exception.Message;
    }
}
