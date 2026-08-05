using System.Text.RegularExpressions;

namespace Microsoft.Agents.ApiCompat;

public sealed record ParsedDiagnostic(
    string Id,
    string Target,
    string Detail,
    string? TargetFramework,
    ApiDifferenceDirection Direction);

public static partial class ApiCompatParser
{
    private const string BreakingChangesSummary =
        "API breaking changes found. If those are intentional, the APICompat suppression file can be updated by specifying the '--generate-suppression-file' parameter.";

    public static IReadOnlyList<ParsedDiagnostic> Parse(ApiCompatExecution execution, bool strict)
    {
        ArgumentNullException.ThrowIfNull(execution);

        var diagnostics = new List<ParsedDiagnostic>();
        var seen = new HashSet<(string Id, string Target, ApiDifferenceDirection Direction, string? TargetFramework)>();
        var unparseableDiagnostics = new List<string>();
        var reportedBreakingChanges = false;

        foreach (var line in EnumerateLines(execution.StandardOutput, execution.StandardError))
        {
            if (string.Equals(line, BreakingChangesSummary, StringComparison.Ordinal))
            {
                reportedBreakingChanges = true;
                continue;
            }

            var diagnosticMatch = DiagnosticRegex().Match(line);
            if (!diagnosticMatch.Success)
            {
                if (DiagnosticPrefixRegex().IsMatch(line))
                {
                    unparseableDiagnostics.Add(line);
                }

                continue;
            }

            var id = diagnosticMatch.Groups["id"].Value;
            var detail = diagnosticMatch.Groups["detail"].Value.Trim();
            var targetMatch = TargetRegex().Match(detail);
            if (!targetMatch.Success)
            {
                unparseableDiagnostics.Add(line);
                continue;
            }

            var direction = ApiDifferenceDirection.BaselineToCandidate;
            if (CandidateOnlyRegex().IsMatch(detail))
            {
                if (!strict)
                {
                    throw new InvalidDataException(
                        $"ApiCompat emitted candidate-only diagnostics during a non-strict parse: {line}");
                }

                direction = ApiDifferenceDirection.CandidateAddition;
            }
            else if (!BaselineToCandidateRegex().IsMatch(detail))
            {
                unparseableDiagnostics.Add(line);
                continue;
            }

            var target = targetMatch.Groups["target"].Value;
            var targetFramework = ExtractTargetFramework(detail);
            var identity = (id, target, direction, targetFramework);
            if (seen.Add(identity))
            {
                diagnostics.Add(new(id, target, detail, targetFramework, direction));
            }
        }

        if (unparseableDiagnostics.Count > 0)
        {
            throw new InvalidDataException(
                $"ApiCompat produced unparseable diagnostics:{Environment.NewLine}{string.Join(Environment.NewLine, unparseableDiagnostics)}");
        }

        if (reportedBreakingChanges && diagnostics.Count == 0)
        {
            throw new InvalidDataException("ApiCompat reported breaking changes without parseable diagnostics.");
        }

        if (execution.ExitCode != 0 && diagnostics.Count == 0)
        {
            throw new InvalidDataException(
                $"ApiCompat exited with {execution.ExitCode} without parseable diagnostics: {execution.StandardError}");
        }

        return diagnostics;
    }

    private static IEnumerable<string> EnumerateLines(params string[] outputs)
    {
        foreach (var output in outputs)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                continue;
            }

            using var reader = new StringReader(output);
            while (reader.ReadLine() is { } line)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    yield return line.Trim();
                }
            }
        }
    }

    private static string? ExtractTargetFramework(string detail)
    {
        var match = TargetFrameworkRegex().Match(detail);
        return match.Success ? match.Groups["targetFramework"].Value : null;
    }

    [GeneratedRegex(@"^(?<id>CP\d{4}|PKV\d{3}): (?<detail>.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex DiagnosticRegex();

    [GeneratedRegex(@"^(CP\d{4}|PKV\d{3}): ", RegexOptions.CultureInvariant)]
    private static partial Regex DiagnosticPrefixRegex();

    [GeneratedRegex(@"^(?:Member|Parameter name on member) '(?<target>[^']+)'", RegexOptions.CultureInvariant)]
    private static partial Regex TargetRegex();

    [GeneratedRegex(@"^Member '[^']+' exists on \[Baseline\] lib/[^ ]+ but not on lib/[^ ]+$|^Parameter name on member '[^']+' changed from '[^']+' to '[^']+'\.$", RegexOptions.CultureInvariant)]
    private static partial Regex BaselineToCandidateRegex();

    [GeneratedRegex(@"^Member '[^']+' exists on lib/[^ ]+ but not on \[Baseline\] lib/[^ ]+$", RegexOptions.CultureInvariant)]
    private static partial Regex CandidateOnlyRegex();

    [GeneratedRegex(@"lib/(?<targetFramework>[^/]+)/[^ ]+", RegexOptions.CultureInvariant)]
    private static partial Regex TargetFrameworkRegex();
}
