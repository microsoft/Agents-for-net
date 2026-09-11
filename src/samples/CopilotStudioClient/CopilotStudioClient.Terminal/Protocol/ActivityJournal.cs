using System.Text.Json;
using Microsoft.Agents.Core.Models;

internal delegate void ActivityRecordAddedHandler(object? sender, ActivityRecord record);

internal sealed class ActivityJournal
{
    private readonly object _gate = new();
    private readonly List<ActivityRecord> _records = [];
    private readonly Func<Activity, string> _formatter;
    private long _nextSequence;

    internal ActivityJournal(Func<Activity, string>? formatter = null)
    {
        _formatter = formatter ?? ActivityJsonFormatter.Format;
    }

    internal event ActivityRecordAddedHandler? RecordAdded;

    internal ActivityRecord Append(Activity activity, ActivityDirection direction)
    {
        ArgumentNullException.ThrowIfNull(activity);

        string? json = null;
        Exception? serializationError = null;
        try
        {
            json = _formatter(activity);
        }
        catch (Exception exception) when (
            exception is JsonException
            or NotSupportedException
            or InvalidOperationException)
        {
            serializationError = exception;
        }

        ActivityRecord record = AddRecord(
            direction,
            activity.Type ?? "unknown",
            GetSummary(activity),
            activity,
            json,
            null);
        RecordAdded?.Invoke(this, record);

        if (serializationError is not null)
        {
            AppendDiagnostic(
                $"Unable to format activity JSON: {serializationError.Message}",
                DiagnosticSeverity.Error);
        }

        return record;
    }

    internal ActivityRecord AppendDiagnostic(string message, DiagnosticSeverity severity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        ActivityRecord record = AddRecord(
            ActivityDirection.Diagnostic,
            "diagnostic",
            message,
            null,
            null,
            severity);
        RecordAdded?.Invoke(this, record);
        return record;
    }

    internal IReadOnlyList<ActivityRecord> Snapshot()
    {
        lock (_gate)
        {
            return _records.ToArray();
        }
    }

    private ActivityRecord AddRecord(
        ActivityDirection direction,
        string type,
        string summary,
        Activity? activity,
        string? json,
        DiagnosticSeverity? severity)
    {
        lock (_gate)
        {
            ActivityRecord record = new(
                ++_nextSequence,
                direction,
                DateTimeOffset.UtcNow,
                type,
                summary,
                activity,
                json,
                severity);

            _records.Add(record);
            return record;
        }
    }

    private static string GetSummary(Activity activity)
    {
        if (!string.IsNullOrWhiteSpace(activity.Summary))
        {
            return activity.Summary;
        }

        if (!string.IsNullOrWhiteSpace(activity.Text))
        {
            return activity.Text;
        }

        if (!string.IsNullOrWhiteSpace(activity.Name))
        {
            return activity.Name;
        }

        if (!string.IsNullOrWhiteSpace(activity.Type))
        {
            return activity.Type;
        }

        return "activity";
    }
}
