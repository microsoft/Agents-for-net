#nullable enable

using System.Collections.Generic;

internal sealed class FakeTerminalView : ITerminalView
{
    public List<ActivityRecord> Activities { get; } = [];

    public List<IReadOnlyList<ChatChange>> ChatChanges { get; } = [];

    public List<bool> BusyStates { get; } = [];

    public List<(string Text, DiagnosticSeverity Severity)> Statuses { get; } = [];

    public List<string> CallOrder { get; } = [];

    public void AddActivity(ActivityRecord record)
    {
        Activities.Add(record);
        CallOrder.Add($"AddActivity:{record.Sequence}");
    }

    public void ApplyChatChanges(IReadOnlyList<ChatChange> changes)
    {
        ChatChanges.Add(changes);
        CallOrder.Add("ApplyChatChanges");
    }

    public void SetBusy(bool isBusy)
    {
        BusyStates.Add(isBusy);
        CallOrder.Add($"SetBusy:{isBusy}");
    }

    public void SetStatus(string text, DiagnosticSeverity severity)
    {
        Statuses.Add((text, severity));
        CallOrder.Add($"SetStatus:{severity}:{text}");
    }
}
