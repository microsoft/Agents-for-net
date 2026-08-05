using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Agents.ApiCompat;

return await Cli.RunAsync(args, CancellationToken.None).ConfigureAwait(false);

namespace Microsoft.Agents.ApiCompat
{
    /// <summary>
    /// Command-line entry point for the API compatibility tool. Parsing is intentionally
    /// dependency-free so the tool stays trivially buildable inside the composite action.
    /// </summary>
    public static class Cli
    {
        private const long MaxReportBytes = 5L * 1024 * 1024;
        private const int MaxErrorLength = 8_000;

        private static readonly JsonSerializerOptions ReportReadOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() },
        };

        public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(args);

            return (args.Length == 0 ? null : args[0]) switch
            {
                "analyze" => await AnalyzeAsync(ParseOptions(args[1..]), cancellationToken).ConfigureAwait(false),
                "render-comment" => await RenderCommentAsync(ParseOptions(args[1..]), cancellationToken).ConfigureAwait(false),
                "--help" or "-h" or "help" => Usage(0),
                _ => Usage(2),
            };
        }

        private static async Task<int> AnalyzeAsync(
            IReadOnlyDictionary<string, string> options,
            CancellationToken cancellationToken)
        {
            if (!TryGetRequired(options, "repo-root", out var repoRoot) ||
                !TryGetRequired(options, "packages", out var packages) ||
                !TryGetRequired(options, "event", out var eventPath) ||
                !TryGetRequired(options, "output", out var output))
            {
                return Fail(2, "Usage: analyze --repo-root <path> --packages <path> --event <path> --output <path> [--candidate-error-file <path>]");
            }

            var outputDirectory = Path.GetFullPath(output);

            PullRequestEvent pullRequest;
            try
            {
                pullRequest = PullRequestEvent.Load(eventPath);
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or JsonException)
            {
                return Fail(1, $"Failed to load pull request event: {exception.Message}");
            }

            try
            {
                var report = await BuildReportAsync(options, repoRoot, packages, outputDirectory, pullRequest, cancellationToken)
                    .ConfigureAwait(false);

                await ReportWriter.WriteAsync(report, outputDirectory, cancellationToken).ConfigureAwait(false);
                WriteGitHubOutputs(report, outputDirectory);
                return 0;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return Fail(1, $"Analysis failed before a report could be written: {exception.Message}");
            }
        }

        private static async Task<CompatibilityReport> BuildReportAsync(
            IReadOnlyDictionary<string, string> options,
            string repoRoot,
            string packages,
            string output,
            PullRequestEvent pullRequest,
            CancellationToken cancellationToken)
        {
            if (options.TryGetValue("candidate-error-file", out var errorFile) &&
                !string.IsNullOrWhiteSpace(errorFile) &&
                File.Exists(errorFile))
            {
                var sanitized = SanitizeError(await File.ReadAllTextAsync(errorFile, cancellationToken).ConfigureAwait(false));
                if (sanitized.Length > 0)
                {
                    return BuildInfrastructureFailure(pullRequest, sanitized);
                }
            }

            using var httpClient = new HttpClient();
            var analyzer = new CompatibilityAnalyzer(new NuGetBaselineResolver(httpClient));
            return await analyzer
                .AnalyzeAsync(new AnalysisOptions(repoRoot, packages, output, pullRequest), cancellationToken)
                .ConfigureAwait(false);
        }

        private static CompatibilityReport BuildInfrastructureFailure(PullRequestEvent pullRequest, string sanitizedError)
        {
            return new CompatibilityReport(
                SchemaVersion: 1,
                RunId: pullRequest.RunId,
                PullRequestNumber: pullRequest.Number,
                BaseRef: pullRequest.BaseRef,
                Decision: AnalysisDecision.InfrastructureFailure,
                Override: OverridePolicy.Evaluate(pullRequest.Labels, pullRequest.Body),
                Packages: Array.Empty<PackageCompatibilityReport>(),
                InfrastructureErrors: new[] { $"Candidate restore or pack failed.\n{sanitizedError}" });
        }

        private static async Task<int> RenderCommentAsync(
            IReadOnlyDictionary<string, string> options,
            CancellationToken cancellationToken)
        {
            if (!TryGetRequired(options, "report", out var reportPath) ||
                !TryGetRequired(options, "output", out var outputPath) ||
                !TryGetRequired(options, "run-id", out var runIdText) ||
                !TryGetRequired(options, "pr-number", out var prNumberText))
            {
                return Fail(2, "Usage: render-comment --report <path> --output <path> --run-id <id> --pr-number <number>");
            }

            if (!long.TryParse(runIdText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var runId))
            {
                return Fail(2, "--run-id must be an integer.");
            }

            if (!int.TryParse(prNumberText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var prNumber))
            {
                return Fail(2, "--pr-number must be an integer.");
            }

            var info = new FileInfo(reportPath);
            if (!info.Exists)
            {
                return Fail(1, $"Report '{reportPath}' does not exist.");
            }

            if (info.Length > MaxReportBytes)
            {
                return Fail(1, $"Report exceeds the {MaxReportBytes} byte limit.");
            }

            CompatibilityReport? report;
            try
            {
                var json = await File.ReadAllTextAsync(reportPath, cancellationToken).ConfigureAwait(false);
                report = JsonSerializer.Deserialize<CompatibilityReport>(json, ReportReadOptions);
            }
            catch (JsonException exception)
            {
                return Fail(1, $"Report is not valid JSON: {exception.Message}");
            }

            if (report is null)
            {
                return Fail(1, "Report deserialized to null.");
            }

            if (report.SchemaVersion != 1)
            {
                return Fail(1, $"Unsupported report schema version {report.SchemaVersion}.");
            }

            var validationError = ValidateRenderReport(report);
            if (validationError is not null)
            {
                return Fail(1, validationError);
            }

            if (report.RunId != runId)
            {
                return Fail(1, "Report run id does not match --run-id.");
            }

            if (report.PullRequestNumber != prNumber)
            {
                return Fail(1, "Report pull request number does not match --pr-number.");
            }

            var comment = ReportWriter.RenderStickyComment(report);
            var outputParent = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!string.IsNullOrEmpty(outputParent))
            {
                Directory.CreateDirectory(outputParent);
            }

            await File.WriteAllTextAsync(outputPath, comment, cancellationToken).ConfigureAwait(false);
            return 0;
        }

        private static string? ValidateRenderReport(CompatibilityReport report)
        {
            if (string.IsNullOrWhiteSpace(report.BaseRef))
            {
                return "Report BaseRef must be non-empty.";
            }

            if (report.Override is null)
            {
                return "Report Override is required.";
            }

            if (string.IsNullOrWhiteSpace(report.Override.Reason))
            {
                return "Report Override.Reason must be non-empty.";
            }

            if (report.Override.Justification is not null &&
                string.IsNullOrWhiteSpace(report.Override.Justification))
            {
                return "Report Override.Justification must be non-empty when provided.";
            }

            if (report.Packages is null)
            {
                return "Report Packages is required.";
            }

            for (var packageIndex = 0; packageIndex < report.Packages.Count; packageIndex++)
            {
                var package = report.Packages[packageIndex];
                if (package is null)
                {
                    return $"Report Packages[{packageIndex}] is required.";
                }

                var packagePrefix = $"Report Packages[{packageIndex}]";
                var packageIdError = ValidateRequiredString(package.PackageId, $"{packagePrefix}.PackageId");
                if (packageIdError is not null)
                {
                    return packageIdError;
                }

                var candidateVersionError = ValidateRequiredString(package.CandidateVersion, $"{packagePrefix}.CandidateVersion");
                if (candidateVersionError is not null)
                {
                    return candidateVersionError;
                }

                var statusError = ValidateRequiredString(package.Status, $"{packagePrefix}.Status");
                if (statusError is not null)
                {
                    return statusError;
                }

                if (package.Findings is null)
                {
                    return $"{packagePrefix}.Findings is required.";
                }

                for (var findingIndex = 0; findingIndex < package.Findings.Count; findingIndex++)
                {
                    var finding = package.Findings[findingIndex];
                    if (finding is null)
                    {
                        return $"{packagePrefix}.Findings[{findingIndex}] is required.";
                    }

                    var findingPrefix = $"{packagePrefix}.Findings[{findingIndex}]";
                    foreach (var error in new[]
                    {
                        ValidateRequiredString(finding.PackageId, $"{findingPrefix}.PackageId"),
                        ValidateRequiredString(finding.BaselineVersion, $"{findingPrefix}.BaselineVersion"),
                        ValidateRequiredString(finding.CandidateVersion, $"{findingPrefix}.CandidateVersion"),
                        ValidateRequiredString(finding.DiagnosticId, $"{findingPrefix}.DiagnosticId"),
                        ValidateRequiredString(finding.Target, $"{findingPrefix}.Target"),
                        ValidateRequiredString(finding.Detail, $"{findingPrefix}.Detail"),
                    })
                    {
                        if (error is not null)
                        {
                            return error;
                        }
                    }
                }
            }

            if (report.InfrastructureErrors is null)
            {
                return "Report InfrastructureErrors is required.";
            }

            for (var errorIndex = 0; errorIndex < report.InfrastructureErrors.Count; errorIndex++)
            {
                var infrastructureError = report.InfrastructureErrors[errorIndex];
                if (string.IsNullOrWhiteSpace(infrastructureError))
                {
                    return $"Report InfrastructureErrors[{errorIndex}] must be non-empty.";
                }
            }

            return null;
        }

        private static string? ValidateRequiredString(string? value, string path) =>
            string.IsNullOrWhiteSpace(value) ? $"{path} must be non-empty." : null;

        private static void WriteGitHubOutputs(CompatibilityReport report, string outputDirectory)
        {
            var path = Environment.GetEnvironmentVariable("GITHUB_OUTPUT");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var findings = report.Packages.SelectMany(package => package.Findings).ToArray();
            var blocking = findings.Count(finding => finding.Severity == FindingSeverity.Blocking);
            var warning = findings.Count(finding => finding.Severity == FindingSeverity.Warning);

            File.AppendAllLines(path, new[]
            {
                $"decision={report.Decision}",
                $"blocking-count={blocking.ToString(CultureInfo.InvariantCulture)}",
                $"warning-count={warning.ToString(CultureInfo.InvariantCulture)}",
                $"report-directory={Path.GetFullPath(outputDirectory)}",
            });
        }

        private static Dictionary<string, string> ParseOptions(string[] args)
        {
            var options = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var index = 0; index < args.Length; index++)
            {
                var argument = args[index];
                if (!argument.StartsWith("--", StringComparison.Ordinal))
                {
                    continue;
                }

                var name = argument[2..];
                var separator = name.IndexOf('=', StringComparison.Ordinal);
                if (separator >= 0)
                {
                    options[name[..separator]] = name[(separator + 1)..];
                    continue;
                }

                if (index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    options[name] = args[++index];
                }
                else
                {
                    options[name] = string.Empty;
                }
            }

            return options;
        }

        private static bool TryGetRequired(IReadOnlyDictionary<string, string> options, string name, out string value)
        {
            if (options.TryGetValue(name, out var candidate) && !string.IsNullOrWhiteSpace(candidate))
            {
                value = candidate;
                return true;
            }

            value = string.Empty;
            return false;
        }

        private static string SanitizeError(string raw)
        {
            var normalized = raw.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            var builder = new StringBuilder(normalized.Length);
            foreach (var character in normalized)
            {
                if (character is '\n' or '\t' || !char.IsControl(character))
                {
                    builder.Append(character);
                }
            }

            var sanitized = builder.ToString().Trim();
            return sanitized.Length > MaxErrorLength ? sanitized[..MaxErrorLength] : sanitized;
        }

        private static int Fail(int exitCode, string message)
        {
            Console.Error.WriteLine(message);
            return exitCode;
        }

        private static int Usage(int exitCode)
        {
            var writer = exitCode == 0 ? Console.Out : Console.Error;
            writer.WriteLine("Microsoft.Agents.ApiCompat");
            writer.WriteLine();
            writer.WriteLine("Commands:");
            writer.WriteLine("  analyze --repo-root <path> --packages <path> --event <path> --output <path> [--candidate-error-file <path>]");
            writer.WriteLine("  render-comment --report <path> --output <path> --run-id <id> --pr-number <number>");
            return exitCode;
        }
    }
}
