using Microsoft.Agents.Core.Models;

internal enum ActivityDirection
{
    Inbound,
    Outbound,
    Diagnostic
}

internal enum DiagnosticSeverity
{
    Information,
    Warning,
    Error
}

internal sealed record ActivityRecord(
    long Sequence,
    ActivityDirection Direction,
    DateTimeOffset Timestamp,
    string Type,
    string Summary,
    Activity? Activity,
    string? Json,
    DiagnosticSeverity? Severity);
