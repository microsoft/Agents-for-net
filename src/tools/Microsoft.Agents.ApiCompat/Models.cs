namespace Microsoft.Agents.ApiCompat;

public enum CompatibilityCategory
{
    Source,
    Binary,
    SourceAndBinary,
    PotentialSourceRisk,
    Infrastructure,
}

public enum FindingSeverity
{
    Blocking,
    Warning,
    Informational,
}

public enum AnalysisDecision
{
    Pass,
    Block,
    Overridden,
    InfrastructureFailure,
}

public enum ApiDifferenceDirection
{
    BaselineToCandidate,
    CandidateAddition,
}

public sealed record Classification(CompatibilityCategory Category, FindingSeverity Severity);

public sealed record CompatibilityFinding(
    string PackageId,
    string BaselineVersion,
    string CandidateVersion,
    string? TargetFramework,
    string DiagnosticId,
    string Target,
    string Detail,
    CompatibilityCategory Category,
    FindingSeverity Severity);

public sealed record PackageCompatibilityReport(
    string PackageId,
    string CandidateVersion,
    string? BaselineVersion,
    string Status,
    IReadOnlyList<CompatibilityFinding> Findings);

public sealed record OverrideResult(bool IsValid, string? Justification, string Reason);

public sealed record CompatibilityReport(
    int SchemaVersion,
    long RunId,
    int PullRequestNumber,
    string BaseRef,
    AnalysisDecision Decision,
    OverrideResult Override,
    IReadOnlyList<PackageCompatibilityReport> Packages,
    IReadOnlyList<string> InfrastructureErrors);
