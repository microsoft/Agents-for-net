#nullable enable

using System.Collections.Generic;

internal interface ITerminalView
{
    void AddActivity(ActivityRecord record);

    void ApplyChatChanges(IReadOnlyList<ChatChange> changes);

    void SetBusy(bool isBusy);

    void SetStatus(string text, DiagnosticSeverity severity);
}
