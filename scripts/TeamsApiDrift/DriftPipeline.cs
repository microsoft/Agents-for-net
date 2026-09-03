using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Microsoft.Agents.TeamsApiDrift;

public static class VersionResolver
{
    public static string Resolve(TextReader reader, string packageId = PackageConstants.PackageId)
    {
        var document = XDocument.Load(reader);
        var version = document.Descendants("PackageVersion")
            .FirstOrDefault(element => string.Equals((string?)element.Attribute("Include"), packageId, StringComparison.OrdinalIgnoreCase))
            ?.Attribute("Version")?.Value;
        if (string.IsNullOrWhiteSpace(version)) throw new InvalidDataException($"PackageVersion for {packageId} was not found.");
        if (!version.StartsWith("$(", StringComparison.Ordinal)) return version;
        var propertyName = version[2..^1];
        var property = document.Descendants(propertyName).FirstOrDefault()?.Value;
        return !string.IsNullOrWhiteSpace(property)
            ? property
            : throw new InvalidDataException($"MSBuild property {propertyName} was not found.");
    }
}

public static class FindingClassifier
{
    public static FindingsResult Classify(ApiComparison comparison, UsageManifest manifest, CapabilityDocument capabilities)
    {
        var findings = comparison.Changes.Select(change => CreateFinding(change, manifest, capabilities)).ToArray();
        return new FindingsResult(
            1,
            PackageConstants.PackageId,
            comparison.FromVersion,
            comparison.ToVersion,
            new FindingSummary(
                findings.Count(item => item.Classification == "blocking"),
                findings.Count(item => item.Classification == "required"),
                findings.Count(item => item.Classification == "review"),
                findings.Count(item => item.Classification == "no-action")),
            findings);
    }

    private static Finding CreateFinding(ApiChange change, UsageManifest manifest, CapabilityDocument capabilities)
    {
        var usages = manifest.Usages.Where(usage => usage.UpstreamSymbol == change.Symbol &&
            (change.Member is null || usage.Members.Count == 0 || usage.Members.Contains(change.Member, StringComparer.Ordinal))).ToArray();
        var capability = MatchCapability(change.Symbol, capabilities);
        var additive = change.Kind is "symbol-added" or "member-added" or "framework-asset-added";
        string? category = null;
        string classification;
        if (usages.Length == 0)
        {
            if (additive && capability.Value is not null && capability.Value.AdoptionPolicy != "advisory-only")
            {
                classification = "review";
                category = capability.Value.AdoptionPolicy == "review-new-members" ? "feature-review" : "internal-opportunity";
            }
            else
            {
                classification = "no-action";
            }
        }
        else if (change.Kind is "symbol-removed" or "member-removed" or "framework-asset-removed" or "symbol-kind-changed")
        {
            classification = "blocking";
        }
        else if (change.Kind is "deprecation-added" or "deprecation-removed")
        {
            classification = "review";
        }
        else if (change.Compatibility == "potentially-breaking")
        {
            classification = usages.Any(usage => usage.Exposure == "publicly-exposed") ? "blocking" : "required";
        }
        else
        {
            classification = "review";
        }

        var exposure = usages.Any(usage => usage.Exposure == "publicly-exposed") ? "publicly-exposed" :
            usages.FirstOrDefault()?.Exposure ?? "unknown";
        return new Finding(
            change.Id,
            classification,
            category,
            change.Kind,
            change.Symbol,
            change.Member,
            capability.Key,
            exposure,
            usages.SelectMany(usage => usage.Files).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            change.Before,
            change.After,
            change.TargetFrameworks,
            change.Evidence.Concat(usages.Length > 0 ? ["dependency-usage"] : []).Concat(capability.Value is not null ? ["teams-capabilities"] : []).Distinct().ToArray(),
            RecommendedAction(classification, category, change));
    }

    private static KeyValuePair<string?, Capability?> MatchCapability(string symbol, CapabilityDocument document)
    {
        return document.Capabilities
            .Select(item => new
            {
                item.Key,
                item.Value,
                Score = item.Value.UpstreamTypes.Contains(symbol, StringComparer.Ordinal)
                    ? int.MaxValue
                    : item.Value.UpstreamNamespaces.Where(prefix => symbol.StartsWith(prefix + ".", StringComparison.Ordinal) || symbol == prefix).Select(prefix => prefix.Length).DefaultIfEmpty(-1).Max()
            })
            .Where(item => item.Score >= 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => new KeyValuePair<string?, Capability?>(item.Key, item.Value))
            .FirstOrDefault();
    }

    private static string RecommendedAction(string classification, string? category, ApiChange change) => classification switch
    {
        "blocking" => $"Adapt or remove the use of {change.Symbol}{(change.Member is null ? string.Empty : $".{change.Member}")} before adopting the candidate version.",
        "required" => "Review the affected .NET contract and update the extension mapping or nullability handling.",
        "review" when category == "feature-review" => "Review the additive upstream API for adoption by the owning Teams extension feature.",
        "review" when category == "internal-opportunity" => "Consider whether the additive API can improve the extension's internal implementation.",
        "review" => "Review the upstream change for behavioral or public API impact.",
        _ => "No recorded extension usage intersects this change."
    };
}

public static class DeterministicReportRenderer
{
    public static string Render(FindingsResult findings, TestSummary? summary = null)
    {
        var lines = new List<string>
        {
            "# Microsoft.Teams.Apps Deterministic Impact Report", "",
            "This report is generated from deterministic artifacts only; it contains no AI-generated conclusions.", "",
            "## Executive summary", "",
            $"Compared **{findings.FromVersion}** to **{findings.ToVersion}** for `{PackageConstants.PackageId}`.", "",
            $"**{findings.Summary.Blocking} blocking**, **{findings.Summary.Required} required**, **{findings.Summary.Review} review**, and **{findings.Summary.NoAction} no-action** finding(s).", "",
            "## Build and test status", ""
        };
        if (summary?.Checks.Count > 0)
        {
            lines.AddRange(new[] { "| Check | Status |", "| --- | --- |" });
            lines.AddRange(summary.Checks.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => $"| {Cell(item.Key)} | {Cell(item.Value)} |"));
        }
        else
        {
            lines.Add("No build or test summary was supplied.");
        }

        AddSection(lines, "Blocking compatibility issues", findings.Findings.Where(item => item.Classification == "blocking"), "No blocking compatibility issues were detected.");
        AddSection(lines, "Required adaptations", findings.Findings.Where(item => item.Classification == "required"), "No required adaptations were detected.");
        AddSection(lines, "Feature-review candidates", findings.Findings.Where(item => item.Category == "feature-review"), "No feature-review candidates were identified.");
        AddSection(lines, "Internal implementation opportunities", findings.Findings.Where(item => item.Category == "internal-opportunity"), "No internal implementation opportunities were identified.");
        AddSection(lines, "Maintainer decisions required", findings.Findings.Where(item => item.Classification == "review" && item.Category is null), "No maintainer decisions are required.");
        AddSection(lines, "No-action upstream changes", findings.Findings.Where(item => item.Classification == "no-action"), "No no-action changes were recorded.");
        lines.AddRange(["", "## Suggested checklist", ""]);
        var actionable = findings.Findings.Where(item => item.Classification != "no-action").OrderBy(item => item.Id).ToArray();
        lines.AddRange(actionable.Length == 0
            ? new[] { "- [x] No direct compatibility work is required." }
            : actionable.Select(item => $"- [ ] **{item.Id}** — {item.RecommendedAction}"));
        lines.Add(string.Empty);
        return string.Join("\n", lines);
    }

    private static void AddSection(List<string> lines, string title, IEnumerable<Finding> source, string empty)
    {
        lines.AddRange(["", $"## {title}", ""]);
        var findings = source.OrderBy(item => item.Capability ?? "Unmapped", StringComparer.Ordinal).ThenBy(item => item.Id).ToArray();
        if (findings.Length == 0)
        {
            lines.Add(empty);
            return;
        }
        foreach (var finding in findings)
        {
            var symbol = finding.Member is null ? finding.UpstreamSymbol : $"{finding.UpstreamSymbol}.{finding.Member}";
            lines.Add($"- **{finding.Id}** — `{symbol}` ({finding.Kind}; {finding.Exposure}). {finding.RecommendedAction}");
            lines.Add($"  - Frameworks: {string.Join(", ", finding.TargetFrameworks)}");
            lines.Add($"  - Files: {(finding.AffectedFiles.Count == 0 ? "No direct source file" : string.Join(", ", finding.AffectedFiles.Select(Paths.Normalize)))}");
            lines.Add($"  - Evidence: {string.Join(", ", finding.Evidence)}");
        }
    }

    private static string Cell(string value) => value.Replace("|", "\\|", StringComparison.Ordinal).ReplaceLineEndings(" ");
}

public static partial class AgentContextBuilder
{
    private const int MaximumSourceLength = 12_000;

    public static object Build(FindingsResult findings, UsageManifest manifest, CapabilityDocument capabilities, string deterministicReport, TestSummary? testSummary, string repositoryRoot)
    {
        var sourceRoot = Path.Combine(repositoryRoot, PackageConstants.SourceRoot);
        var omitted = new List<string>();
        var files = new List<object>();
        foreach (var path in findings.Findings.Where(item => item.Classification != "no-action").SelectMany(item => item.AffectedFiles).Distinct().Order(StringComparer.Ordinal))
        {
            var fullPath = Path.GetFullPath(Path.Combine(repositoryRoot, path));
            if (!Paths.IsContainedBy(sourceRoot, fullPath))
            {
                throw new InvalidDataException($"Source path escapes the MSTeams source root: {path}");
            }
            RejectSymlinkTraversal(sourceRoot, fullPath, path);
            if (!File.Exists(fullPath) || Path.GetExtension(fullPath) != ".cs")
            {
                omitted.Add(path);
                continue;
            }
            var content = Redact(File.ReadAllText(fullPath));
            files.Add(new { path = Paths.Normalize(path), content = content[..Math.Min(content.Length, MaximumSourceLength)], truncated = content.Length > MaximumSourceLength });
        }

        return new
        {
            schemaVersion = 1,
            package = "Microsoft.Agents.Extensions.MSTeams",
            dependency = PackageConstants.PackageId,
            authoritativeArtifacts = new { findings, usageManifest = manifest, capabilities, deterministicReport, testSummary },
            relevantSourceFiles = files,
            omittedSourceFiles = omitted
        };
    }

    private static void RejectSymlinkTraversal(string sourceRoot, string fullPath, string path)
    {
        var current = Path.GetFullPath(sourceRoot);
        RejectSymlink(current, path);
        var relative = Path.GetRelativePath(current, fullPath);
        foreach (var component in relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, component);
            RejectSymlink(current, path);
        }
    }

    private static void RejectSymlink(string fullPath, string path)
    {
        FileSystemInfo entry = Directory.Exists(fullPath) ? new DirectoryInfo(fullPath) : new FileInfo(fullPath);
        if (entry.LinkTarget is not null)
        {
            throw new InvalidDataException($"Source path contains a symlink: {path}");
        }
    }

    internal static string Redact(string content)
    {
        content = AuthorizationRegex().Replace(content, "$1[REDACTED]");
        return SecretRegex().Replace(content, "$1[REDACTED]");
    }

    [GeneratedRegex("(authorization\\s*[:=]\\s*[\\\"']?)(?:bearer\\s+)?[^\\\"'\\s,}]+", RegexOptions.IgnoreCase)]
    private static partial Regex AuthorizationRegex();
    [GeneratedRegex("((?:clientSecret|apiKey|password|token)\\s*[:=]\\s*[\\\"'])[^\\\"']+", RegexOptions.IgnoreCase)]
    private static partial Regex SecretRegex();
}

public static partial class AgentReportValidator
{
    internal static readonly string[] RequiredSections =
    [
        "Summary", "Compatibility breaks", "Required adaptations", "Feature-review candidates",
        "Internal implementation opportunities", "Maintainer decisions", "No action",
        "Suggested implementation issues", "Validation checklist"
    ];

    public static AgentReportValidation Validate(string report, FindingsResult findings)
    {
        var normalized = report.ReplaceLineEndings("\n");
        var errors = new List<string>();
        if (!normalized.StartsWith("# Microsoft.Teams.Apps Impact Report\n", StringComparison.Ordinal)) errors.Add("Report must start with the exact required title.");
        var previous = -1;
        foreach (var section in RequiredSections)
        {
            var heading = $"## {section}";
            var matches = Regex.Matches(
                normalized,
                $"(?m)^{Regex.Escape(heading)}$");
            if (matches.Count == 0) errors.Add($"Missing required section: {section}.");
            else
            {
                if (matches.Count > 1) errors.Add($"Section must appear exactly once: {section}.");
                var index = matches[0].Index;
                if (index < previous) errors.Add("Sections must appear in the required order.");
                previous = index;
            }
        }
        const string advisory = "This is an advisory report; it does not make or authorize implementation decisions.";
        var summaryIndex = normalized.IndexOf("## Summary\n", StringComparison.Ordinal);
        if (summaryIndex >= 0)
        {
            var first = normalized[(summaryIndex + "## Summary\n".Length)..].Split('\n').Select(line => line.Trim()).FirstOrDefault(line => line.Length > 0);
            if (first?.StartsWith(advisory, StringComparison.Ordinal) != true) errors.Add($"Summary must start with: {advisory}");
        }

        var known = findings.Findings.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var referenced = FindingIdRegex().Matches(report).Select(match => match.Value).Distinct().Order(StringComparer.Ordinal).ToArray();
        var unknown = referenced.Where(id => !known.Contains(id)).ToArray();
        var missing = findings.Findings.Where(item => item.Classification is "blocking" or "required").Select(item => item.Id).Where(id => !referenced.Contains(id, StringComparer.Ordinal)).Order(StringComparer.Ordinal).ToArray();
        if (unknown.Length > 0) errors.Add($"Unknown finding ID(s): {string.Join(", ", unknown)}.");
        if (missing.Length > 0) errors.Add($"Missing mandatory finding ID(s): {string.Join(", ", missing)}.");
        var inSuggestedImplementationIssues = false;
        foreach (var line in normalized.Split('\n'))
        {
            if (line == "## Suggested implementation issues")
            {
                inSuggestedImplementationIssues = true;
                continue;
            }

            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                inSuggestedImplementationIssues = false;
                continue;
            }

            if (inSuggestedImplementationIssues && Regex.IsMatch(line, "^[-*+] ") && !FindingIdRegex().IsMatch(line) && !line.StartsWith("- No ", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Action item is not tied to a finding ID: {line}");
            }
        }
        return new AgentReportValidation(1, errors.Count == 0, referenced, missing, unknown, errors);
    }

    [GeneratedRegex("\\bMTAPI-[A-Za-z0-9-]+\\b")]
    private static partial Regex FindingIdRegex();
}
