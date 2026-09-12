using System.Text.Json;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;

internal delegate void ActivityRecordAddedHandler(object? sender, ActivityRecord record);

internal sealed class ActivityJournal
{
    private readonly object _gate = new();
    private readonly List<ActivityRecord> _records = [];
    private readonly Func<Activity, Activity> _cloner;
    private readonly Func<Activity, string> _formatter;
    private long _nextSequence;

    internal ActivityJournal(
        Func<Activity, string>? formatter = null,
        Func<Activity, Activity>? cloner = null)
    {
        _formatter = formatter ?? ActivityJsonFormatter.Format;
        _cloner = cloner ?? SnapshotActivity;
    }

    internal event ActivityRecordAddedHandler? RecordAdded;

    internal ActivityRecord Append(Activity activity, ActivityDirection direction)
    {
        ArgumentNullException.ThrowIfNull(activity);

        Activity? frozenActivity = null;
        string? json = null;
        Exception? serializationError = null;
        try
        {
            frozenActivity = _cloner(activity);
            json = _formatter(frozenActivity);
        }
        catch (Exception exception) when (
            exception is JsonException
            or NotSupportedException
            or InvalidOperationException)
        {
            serializationError = exception;
        }

        Activity sourceActivity = frozenActivity ?? activity;
        ActivityRecord record = AddRecord(
            direction,
            sourceActivity.Type ?? "unknown",
            GetSummary(sourceActivity),
            frozenActivity,
            json,
            null);
        RecordAdded?.Invoke(this, record);

        if (serializationError is not null)
        {
            AppendDiagnostic(
                $"{(frozenActivity is null ? "Unable to snapshot" : "Unable to format")} activity JSON: {serializationError.Message}",
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

    private static Activity SnapshotActivity(Activity activity)
    {
        return ProtocolJsonSerializer.CloneTo<Activity>(activity);
    }
}
