using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.ApiCompat;

public static class ReportWriter
{
    private const int MaxFieldLength = 2_000;
    private const int MaxCommentLength = 60_000;
    private const string TruncationSuffix = "... (truncated)";
    private const string StickyMarker = "<!-- agents-sdk-api-compat -->";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static async Task WriteAsync(
        CompatibilityReport report,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        Directory.CreateDirectory(outputDirectory);

        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "report.json"),
            JsonSerializer.Serialize(Sanitize(report), JsonOptions),
            cancellationToken).ConfigureAwait(false);

        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "report.md"),
            RenderMarkdown(report),
            cancellationToken).ConfigureAwait(false);

        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "summary.md"),
            RenderSummary(report),
            cancellationToken).ConfigureAwait(false);

        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "annotations.txt"),
            RenderAnnotations(report),
            cancellationToken).ConfigureAwait(false);

        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "comment.md"),
            RenderStickyComment(report),
            cancellationToken).ConfigureAwait(false);
    }

    public static string RenderStickyComment(CompatibilityReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var builder = new StringBuilder();
        builder.AppendLine(StickyMarker);
        builder.AppendLine($"<!-- api-compat-run:{report.RunId.ToString(CultureInfo.InvariantCulture)} -->");
        builder.AppendLine("# API compatibility");
        builder.AppendLine();
        builder.AppendLine($"**Decision:** {report.Decision}");
        builder.AppendLine($"**Base ref:** {Escape(report.BaseRef)}");
        builder.AppendLine();

        if (report.InfrastructureErrors.Count > 0)
        {
            builder.AppendLine("### Infrastructure errors");
            foreach (var error in report.InfrastructureErrors)
            {
                builder.AppendLine($"- {Escape(Truncate(error))}");
            }

            builder.AppendLine();
        }

        if (report.Override.IsValid)
        {
            builder.AppendLine("> Breaking changes were approved via override.");
            builder.AppendLine();
        }

        foreach (var package in report.Packages)
        {
            builder.AppendLine($"### {Escape(package.PackageId)} ({FormatVersions(package)}) — {Escape(package.Status)}");

            if (package.Findings.Count == 0)
            {
                builder.AppendLine("- No differences detected.");
                builder.AppendLine();
                continue;
            }

            foreach (var group in package.Findings
                .GroupBy(finding => finding.Category)
                .OrderBy(group => group.Key.ToString(), StringComparer.Ordinal))
            {
                builder.AppendLine($"- **{group.Key}**");
                foreach (var finding in group.OrderBy(finding => finding.DiagnosticId, StringComparer.Ordinal))
                {
                    builder.AppendLine(
                        $"  - `{finding.DiagnosticId}` [{finding.Severity}] `{Escape(Truncate(finding.Target))}` — {Escape(Truncate(finding.Detail))}");
                }
            }

            builder.AppendLine();
        }

        return Cap(builder.ToString().TrimEnd());
    }

    private static string RenderMarkdown(CompatibilityReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# API Compatibility Report");
        builder.AppendLine();
        builder.AppendLine($"- Decision: **{report.Decision}**");
        builder.AppendLine($"- Pull request: #{report.PullRequestNumber}");
        builder.AppendLine($"- Base ref: {Escape(report.BaseRef)}");
        builder.AppendLine($"- Run id: {report.RunId}");
        builder.AppendLine();

        if (report.InfrastructureErrors.Count > 0)
        {
            builder.AppendLine("## Infrastructure errors");
            foreach (var error in report.InfrastructureErrors)
            {
                builder.AppendLine($"- {Escape(Truncate(error))}");
            }

            builder.AppendLine();
        }

        foreach (var package in report.Packages)
        {
            builder.AppendLine($"## {Escape(package.PackageId)} ({FormatVersions(package)})");
            builder.AppendLine($"Status: {Escape(package.Status)}");
            builder.AppendLine();

            if (package.Findings.Count == 0)
            {
                builder.AppendLine("No differences detected.");
                builder.AppendLine();
                continue;
            }

            foreach (var group in package.Findings
                .GroupBy(finding => finding.Category)
                .OrderBy(group => group.Key.ToString(), StringComparer.Ordinal))
            {
                builder.AppendLine($"### {group.Key}");
                foreach (var finding in group.OrderBy(finding => finding.DiagnosticId, StringComparer.Ordinal))
                {
                    var framework = string.IsNullOrEmpty(finding.TargetFramework)
                        ? string.Empty
                        : $" ({Escape(finding.TargetFramework)})";
                    builder.AppendLine(
                        $"- **{finding.DiagnosticId}** [{finding.Severity}]{framework} `{Escape(Truncate(finding.Target))}` — {Escape(Truncate(finding.Detail))}");
                }

                builder.AppendLine();
            }
        }

        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    private static string RenderSummary(CompatibilityReport report)
    {
        var findings = report.Packages.SelectMany(package => package.Findings).ToArray();
        var blocking = findings.Count(finding => finding.Severity == FindingSeverity.Blocking);
        var warnings = findings.Count(finding => finding.Severity == FindingSeverity.Warning);

        var builder = new StringBuilder();
        builder.AppendLine("# API Compatibility Summary");
        builder.AppendLine();
        builder.AppendLine($"- Decision: **{report.Decision}**");
        builder.AppendLine($"- Packages analyzed: {report.Packages.Count}");
        builder.AppendLine($"- Blocking findings: {blocking}");
        builder.AppendLine($"- Warnings: {warnings}");
        builder.AppendLine($"- Infrastructure errors: {report.InfrastructureErrors.Count}");
        if (report.Override.IsValid)
        {
            builder.AppendLine("- Override: approved");
        }

        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    private static string RenderAnnotations(CompatibilityReport report)
    {
        var builder = new StringBuilder();

        foreach (var error in report.InfrastructureErrors)
        {
            builder.AppendLine($"::error title=API compatibility infrastructure failure::{AnnotationEscapeData(Truncate(error))}");
        }

        foreach (var finding in report.Packages.SelectMany(package => package.Findings))
        {
            var command = finding.Severity == FindingSeverity.Blocking ? "error" : "warning";
            var title = AnnotationEscapeProperty($"{finding.DiagnosticId} in {finding.PackageId}");
            var message = AnnotationEscapeData(Truncate($"{finding.Target} — {finding.Detail}"));
            builder.AppendLine($"::{command} title={title}::{message}");
        }

        return builder.ToString();
    }

    private static CompatibilityReport Sanitize(CompatibilityReport report)
    {
        var packages = report.Packages
            .Select(package => package with
            {
                Findings = package.Findings
                    .Select(finding => finding with
                    {
                        Target = Truncate(finding.Target),
                        Detail = Truncate(finding.Detail),
                    })
                    .ToArray(),
            })
            .ToArray();

        var errors = report.InfrastructureErrors.Select(Truncate).ToArray();

        return report with { Packages = packages, InfrastructureErrors = errors };
    }

    private static string FormatVersions(PackageCompatibilityReport package) =>
        $"{Escape(package.BaselineVersion ?? "none")} → {Escape(package.CandidateVersion)}";

    private static string Truncate(string value)
    {
        if (value.Length <= MaxFieldLength)
        {
            return value;
        }

        return value[..(MaxFieldLength - TruncationSuffix.Length)] + TruncationSuffix;
    }

    private static string Cap(string value)
    {
        if (value.Length <= MaxCommentLength)
        {
            return value;
        }

        return value[..(MaxCommentLength - TruncationSuffix.Length)] + TruncationSuffix;
    }

    private static string Escape(string value) => value
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("@", "&#64;", StringComparison.Ordinal)
        .Replace("|", "&#124;", StringComparison.Ordinal);

    private static string AnnotationEscapeData(string value) => value
        .Replace("%", "%25", StringComparison.Ordinal)
        .Replace("\r", "%0D", StringComparison.Ordinal)
        .Replace("\n", "%0A", StringComparison.Ordinal);

    private static string AnnotationEscapeProperty(string value) => AnnotationEscapeData(value)
        .Replace(":", "%3A", StringComparison.Ordinal)
        .Replace(",", "%2C", StringComparison.Ordinal);
}
