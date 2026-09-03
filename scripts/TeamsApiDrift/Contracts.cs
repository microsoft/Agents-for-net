using System.Text.Json.Serialization;

namespace Microsoft.Agents.TeamsApiDrift;

public sealed record ApiModel(
    int SchemaVersion,
    string Package,
    string Version,
    IReadOnlyList<FrameworkApiModel> Frameworks);

public sealed record FrameworkApiModel(
    string TargetFramework,
    string? Asset,
    IReadOnlyList<ApiSymbolModel> Symbols);

public sealed record ApiSymbolModel(
    string Name,
    string Kind,
    string Accessibility,
    string? BaseType,
    IReadOnlyList<string> Interfaces,
    IReadOnlyList<string> GenericConstraints,
    bool Obsolete,
    IReadOnlyList<ApiMemberModel> Members);

public sealed record ApiMemberModel(
    string Key,
    string Name,
    string Kind,
    string Accessibility,
    string Signature,
    bool Obsolete);

public sealed record ApiComparison(
    int SchemaVersion,
    string Package,
    string FromVersion,
    string ToVersion,
    bool Changed,
    IReadOnlyList<ApiChange> Changes);

public sealed record ApiChange(
    string Id,
    string Kind,
    string Symbol,
    string? Member,
    string? Before,
    string? After,
    string Compatibility,
    IReadOnlyList<string> TargetFrameworks,
    IReadOnlyList<string> Evidence);

public sealed class UsageManifest
{
    public int SchemaVersion { get; set; } = 1;
    public string Package { get; set; } = PackageConstants.PackageId;
    public string DeclaredVersion { get; set; } = string.Empty;
    public string SourceRoot { get; set; } = PackageConstants.SourceRoot;
    public List<UsageEntry> Usages { get; set; } = [];
}

public sealed class UsageEntry
{
    public string UpstreamSymbol { get; set; } = string.Empty;
    public List<string> Members { get; set; } = [];
    public List<string> UsageKinds { get; set; } = [];
    public string Exposure { get; set; } = "internal-only";
    public List<string> Files { get; set; } = [];
}

public sealed class CapabilityDocument
{
    public int SchemaVersion { get; set; } = 1;
    public string Package { get; set; } = PackageConstants.PackageId;
    public Dictionary<string, Capability> Capabilities { get; set; } = [];
}

public sealed class Capability
{
    public string Description { get; set; } = string.Empty;
    public List<string> Owners { get; set; } = [];
    public List<string> UpstreamNamespaces { get; set; } = [];
    public List<string> UpstreamTypes { get; set; } = [];
    public string AdoptionPolicy { get; set; } = "advisory-only";
}

public sealed record CollectedUsage(
    int SchemaVersion,
    string Package,
    string Assembly,
    IReadOnlyList<CollectedUsageEntry> Usages,
    IReadOnlyList<string>? SourceFiles = null);

public sealed record CollectedUsageEntry(
    string UpstreamSymbol,
    IReadOnlyList<string> Members,
    string Exposure);

public sealed record UsageValidation(
    int SchemaVersion,
    bool Valid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> MissingSymbols,
    IReadOnlyList<string> MissingMembers);

public sealed record FindingsResult(
    int SchemaVersion,
    string Package,
    string FromVersion,
    string ToVersion,
    FindingSummary Summary,
    IReadOnlyList<Finding> Findings);

public sealed record FindingSummary(
    int Blocking,
    int Required,
    int Review,
    [property: JsonPropertyName("no-action")] int NoAction);

public sealed record Finding(
    string Id,
    string Classification,
    string? Category,
    string Kind,
    string UpstreamSymbol,
    string? Member,
    string? Capability,
    string Exposure,
    IReadOnlyList<string> AffectedFiles,
    string? Before,
    string? After,
    IReadOnlyList<string> TargetFrameworks,
    IReadOnlyList<string> Evidence,
    string RecommendedAction);

public sealed record TestSummary(
    int SchemaVersion,
    IReadOnlyDictionary<string, string> Checks);

public sealed record AgentReportValidation(
    int SchemaVersion,
    bool Valid,
    IReadOnlyList<string> ReferencedFindingIds,
    IReadOnlyList<string> MissingMandatoryFindingIds,
    IReadOnlyList<string> UnknownFindingIds,
    IReadOnlyList<string> Errors);

public static class PackageConstants
{
    public const string PackageId = "Microsoft.Teams.Apps";
    public const string AssemblyName = "Microsoft.Teams.Apps";
    public const string SourceRoot = "src/libraries/Extensions/Microsoft.Agents.Extensions.MSTeams";
    public const string ArtifactDirectory = "artifacts/teams-api-drift";
}
