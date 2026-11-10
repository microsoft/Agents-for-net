using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Microsoft.Agents.TeamsApiDrift;

public sealed record ReviewFile(string Path, string ContentHash, string RepositoryHash);

public sealed record ReviewArtifact(string Path, string ContentHash);

public sealed record ImpactReviewInput(int SchemaVersion, string ReviewId, string Commit, ApiComparison Comparison,
    UsageManifest UsageManifest, CapabilityDocument Capabilities, CollectedUsage? CollectedUsage,
    TestSummary Checks, IReadOnlyList<ReviewFile> Files, IReadOnlyList<ReviewArtifact> Artifacts, int UnmappedSourceFileCount = 0);

public sealed record ReviewCitation(string Path, string? Quote = null, int? StartLine = null, int? EndLine = null);

public sealed record AdoptionAssessment(string Outcome, string Reason);

public sealed record ImpactDecision(string ChangeId, string Verdict, string Usage, string Reason, IReadOnlyList<ReviewCitation> Evidence, AdoptionAssessment? Adoption = null);

public sealed record ImpactReview(int SchemaVersion, string ReviewId, string FromVersion, string ToVersion, IReadOnlyList<ImpactDecision> Decisions);

public sealed record ImpactCounts(int Safe, int NeedsAdaptation, int NeedsHumanReview, int AdoptionRecommendations = 0,
    int AdoptionNotNeeded = 0, int AdoptionNeedsProductDecision = 0);

public sealed record ImpactValidation(int SchemaVersion, bool Valid, bool CanProceed, ImpactCounts Summary,
    IReadOnlyList<string> Errors, IReadOnlyList<ImpactDecision> Decisions);

public static class ImpactReviewPipeline
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        { ".cs", ".csproj", ".props", ".targets", ".md", ".json", ".slnx", ".xml" };
    private const string TestsRoot = "src/tests/Microsoft.Agents.Extensions.MSTeams.Tests/";

    internal static bool RequiresAdoptionAssessment(ApiChange change) => change.Kind is "symbol-added" or "member-added" or "framework-asset-added";

    private static bool CompleteAdoption(AdoptionAssessment? adoption) =>
        adoption is { Outcome: "recommend-adoption" or "no-adoption-needed" or "needs-product-decision" } && !string.IsNullOrWhiteSpace(adoption.Reason);

    internal static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static string Normalize(string text) => text.TrimStart('\uFEFF').ReplaceLineEndings("\n");

    private static string Id(ImpactReviewInput input) => Hash(JsonSerializer.Serialize(input with { ReviewId = "" }, ToolJson.Options));

    internal static string Git(string root, params string[] arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start git.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidDataException($"git failed: {error.GetAwaiter().GetResult()}");
        }

        return output.GetAwaiter().GetResult();
    }

    private static string[] SourcePaths(string root) => Git(root, "ls-files", "-z", "--cached", "--others", "--exclude-standard").Split('\0', StringSplitOptions.RemoveEmptyEntries)
        .Where(path => (path.StartsWith("src/", StringComparison.Ordinal) || path.StartsWith("docs/", StringComparison.Ordinal) || !path.Contains('/')) &&
            Extensions.Contains(System.IO.Path.GetExtension(path)) && System.IO.Path.GetFileName(path) is not ("AGENTS.md" or "CLAUDE.md" or "SKILL.md"))
        .Order(StringComparer.Ordinal).ToArray();

    internal static string Resolve(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains('\\') || path.StartsWith('/') || path.Split('/').Any(part => part is ".." or "." or ""))
        {
            throw new InvalidDataException($"Invalid review path: {path}");
        }

        var full = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, path));

        if (!Paths.IsContainedBy(root, full) || full == System.IO.Path.GetFullPath(root))
        {
            throw new InvalidDataException($"Review path escapes root: {path}");
        }

        AgentContextBuilder.RejectSymlinkTraversal(root, full, path);

        return full;
    }

    internal static (CollectedUsage? Usage, int Unmapped) SnapshotUsage(CollectedUsage? collected, IReadOnlyList<ReviewFile> files)
    {
        if (collected is null)
        {
            return (null, 0);
        }

        var sources = new HashSet<string>(StringComparer.Ordinal);
        var unmapped = 0;

        foreach (var source in collected.SourceFiles ?? [])
        {
            // Portable PDBs can reference a different runner, OS, or SourceLink root.
            // Resolve hints against inventoried paths, never read the original path.
            var normalized = source?.Replace('\\', '/');
            var matches = files.Where(file => file.Path.EndsWith(".cs", StringComparison.Ordinal) &&
                (string.Equals(normalized, file.Path, StringComparison.OrdinalIgnoreCase) ||
                 normalized?.EndsWith("/" + file.Path, StringComparison.OrdinalIgnoreCase) == true)).ToArray();

            if (matches.Length == 1)
            {
                sources.Add(matches[0].Path);
            }
            else
            {
                unmapped++;
            }
        }

        return (collected with { SourceFiles = sources.Order(StringComparer.Ordinal).ToArray() }, unmapped);
    }

    public static ImpactReviewInput Prepare(string repositoryRoot, string workspace, ApiComparison comparison,
        UsageManifest manifest, CapabilityDocument capabilities, CollectedUsage? collected, TestSummary checks, ApiModel before, ApiModel after)
    {
        if (comparison.SchemaVersion != 1 || comparison.Package != PackageConstants.PackageId ||
            comparison.Changes.Select(change => change.Id).Distinct(StringComparer.Ordinal).Count() != comparison.Changes.Count)
        {
            throw new InvalidDataException("Comparison must use schema 1, the Teams package, and unique change IDs.");
        }

        if (JsonSerializer.Serialize(ApiComparer.Compare(before, after), ToolJson.Options) != JsonSerializer.Serialize(comparison, ToolJson.Options))
        {
            throw new InvalidDataException("API models do not reproduce the supplied comparison.");
        }

        var root = System.IO.Path.GetFullPath(repositoryRoot);
        var destination = System.IO.Path.GetFullPath(workspace);
        // Never overwrite a checkout, a previous snapshot, or a symlink destination.
        AgentContextBuilder.RejectSymlinkTraversal(System.IO.Path.GetPathRoot(destination)!, destination, destination);

        if (Directory.Exists(destination) && Directory.EnumerateFileSystemEntries(destination).Any())
        {
            throw new InvalidDataException("Review workspace must be a new or empty directory.");
        }

        if (Paths.IsContainedBy(System.IO.Path.Combine(root, "src"), destination) || Paths.IsContainedBy(System.IO.Path.Combine(root, ".git"), destination))
        {
            throw new InvalidDataException("Review workspace must not be inside source or Git metadata.");
        }

        Directory.CreateDirectory(destination);

        var files = new List<ReviewFile>();

        foreach (var relative in SourcePaths(root))
        {
            var original = Normalize(File.ReadAllText(Resolve(root, relative)));
            var content = Regex.Replace(AgentContextBuilder.Redact(original),
                """("[^"]*(?:secret|password|token|apiKey|connectionString|authorization)[^"]*"\s*:\s*")(?:\\.|[^"\\])*(")""",
                "$1[REDACTED]$2", RegexOptions.IgnoreCase);
            var target = Resolve(destination, relative);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!);
            File.WriteAllText(target, content, new UTF8Encoding(false));
            files.Add(new(relative, Hash(content), Hash(original)));
        }

        if (!files.Any(file => file.Path.StartsWith(PackageConstants.SourceRoot + "/", StringComparison.Ordinal)))
        {
            throw new InvalidDataException("The tracked MSTeams implementation is missing from the review snapshot.");
        }

        var artifacts = new List<ReviewArtifact>();

        foreach (var (path, model) in new[] { ("baseline-api.json", before), ("candidate-api.json", after) })
        {
            var text = Normalize(JsonSerializer.Serialize(model, ToolJson.Options));
            File.WriteAllText(Resolve(destination, path), text, new UTF8Encoding(false));
            artifacts.Add(new(path, Hash(text)));
        }

        var navigation = SnapshotUsage(collected, files);
        var input = new ImpactReviewInput(1, "", Git(root, "rev-parse", "HEAD").Trim(), comparison, manifest, capabilities,
            navigation.Usage, checks, files, artifacts, navigation.Unmapped);
        input = input with { ReviewId = Id(input) };
        ToolJson.Write(System.IO.Path.Combine(destination, "review-input.json"), input);

        return input;
    }

    public static ImpactReview Parse(string text)
    {
        var json = Normalize(text).Trim();
        // Copilot's text mode includes progress messages even with --silent. Accept a
        // single, terminal fenced response; never guess among multiple JSON answers.
        var fences = Regex.Matches(json, @"(?m)^```[^\n]*$");

        if (fences.Count > 0)
        {
            if (fences.Count != 2 || fences[0].Value is not ("```json" or "```") ||
                fences[1].Value != "```" || fences[1].Index + fences[1].Length != json.Length)
            {
                throw new InvalidDataException("Expected one complete terminal JSON fence with no trailing content.");
            }

            var prefix = json[..fences[0].Index].Trim();

            if (prefix.StartsWith('{') || prefix.StartsWith('['))
            {
                throw new InvalidDataException("Multiple response payloads are not allowed.");
            }

            var bodyStart = fences[0].Index + fences[0].Length + 1;
            json = json[bodyStart..fences[1].Index].Trim();
        }

        var options = new JsonSerializerOptions(ToolJson.Options)
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectRequiredConstructorParameters = true
        };

        return JsonSerializer.Deserialize<ImpactReview>(json, options) ?? throw new InvalidDataException("Empty impact review.");
    }

    public static ImpactValidation Validate(ImpactReview review, ImpactReviewInput input, string workspace, string repositoryRoot)
    {
        var errors = new List<string>();

        if (input.SchemaVersion != 1 || review.SchemaVersion != 1 || input.ReviewId != Id(input) || review.ReviewId != input.ReviewId ||
            review.FromVersion != input.Comparison.FromVersion || review.ToVersion != input.Comparison.ToVersion)
        {
            errors.Add("Review identity, schema, or package versions do not match the prepared comparison.");
        }

        if (input.Commit != Git(repositoryRoot, "rev-parse", "HEAD").Trim())
        {
            errors.Add("Repository commit changed after preparation.");
        }

        if (!SourcePaths(repositoryRoot).SequenceEqual(input.Files.Select(file => file.Path)))
        {
            errors.Add("Repository source inventory changed after preparation.");
        }

        foreach (var file in input.Files)
        {
            var snapshot = Resolve(workspace, file.Path);
            var original = Resolve(repositoryRoot, file.Path);

            if (!File.Exists(snapshot) || Hash(Normalize(File.ReadAllText(snapshot))) != file.ContentHash ||
                !File.Exists(original) || Hash(Normalize(File.ReadAllText(original))) != file.RepositoryHash)
            {
                errors.Add($"Source changed after preparation: {file.Path}.");
            }
        }

        foreach (var artifact in input.Artifacts)
        {
            var path = Resolve(workspace, artifact.Path);

            if (!File.Exists(path) || Hash(Normalize(File.ReadAllText(path))) != artifact.ContentHash)
            {
                errors.Add($"Package metadata changed after preparation: {artifact.Path}.");
            }
        }

        var decisions = review.Decisions ?? [];

        if (review.Decisions is null || decisions.Any(decision => decision is null))
        {
            return Invalid(errors.Append("Decisions must be an array of objects.").ToArray());
        }

        if (decisions.GroupBy(decision => decision.ChangeId).Any(group => group.Count() > 1))
        {
            errors.Add("Duplicate change decisions.");
        }

        foreach (var change in input.Comparison.Changes)
        {
            if (!decisions.Any(decision => decision.ChangeId == change.Id))
            {
                errors.Add($"Missing decision: {change.Id}.");
            }
        }

        var resolved = new List<ImpactDecision>();

        foreach (var decision in decisions)
        {
            var change = input.Comparison.Changes.FirstOrDefault(change => change.Id == decision.ChangeId);

            if (change is null)
            {
                errors.Add($"Unknown change: {decision.ChangeId}.");
                continue;
            }

            if (decision.Verdict is not ("safe" or "needs-adaptation" or "needs-human-review"))
            {
                errors.Add($"Invalid verdict: {change.Id}.");
            }

            if (decision.Usage is not ("used" or "unused" or "uncertain"))
            {
                errors.Add($"Invalid usage: {change.Id}.");
            }

            if (string.IsNullOrWhiteSpace(decision.Reason))
            {
                errors.Add($"Missing reasoning: {change.Id}.");
            }

            if (decision.Adoption is null && RequiresAdoptionAssessment(change))
            {
                errors.Add($"Missing adoption assessment for addition: {change.Id}. Supply adoption with outcome and reason; feature adoption remains nonblocking.");
            }

            if (decision.Adoption is { } adoption)
            {
                if (adoption.Outcome is not ("recommend-adoption" or "no-adoption-needed" or "needs-product-decision"))
                {
                    errors.Add($"Invalid adoption outcome: {change.Id}.");
                }

                if (string.IsNullOrWhiteSpace(adoption.Reason))
                {
                    errors.Add($"Missing adoption reasoning: {change.Id}.");
                }
            }

            var citations = new List<ReviewCitation>();

            if (decision.Evidence is null)
            {
                errors.Add($"Evidence must be an array: {change.Id}.");
            }

            foreach (var citation in decision.Evidence ?? [])
            {
                try
                {
                    citations.Add(ResolveCitation(citation, input, workspace));
                }
                catch (InvalidDataException exception)
                {
                    errors.Add($"{change.Id}: {exception.Message}");
                }
            }

            var knownUse = input.UsageManifest.Usages.Any(use => use.UpstreamSymbol == change.Symbol &&
                (change.Member is null || use.Members.Count == 0 || use.Members.Contains(change.Member, StringComparer.Ordinal))) ||
                input.CollectedUsage?.Usages.Any(use => use.UpstreamSymbol == change.Symbol &&
                    (change.Member is null || use.Members.Contains(change.Member, StringComparer.Ordinal))) == true;

            if (decision.Usage == "unused" && knownUse)
            {
                errors.Add($"Recorded dependency usage contradicts unused claim: {change.Id}.");
            }

            if (decision.Verdict is "safe" or "needs-adaptation")
            {
                if (!citations.Any(citation => citation.Path.StartsWith(PackageConstants.SourceRoot + "/", StringComparison.Ordinal) && citation.Path.EndsWith(".cs", StringComparison.Ordinal)))
                {
                    errors.Add($"An implementation citation is required: {change.Id}.");
                }
            }

            if (decision.Verdict == "safe")
            {
                if (decision.Usage == "uncertain")
                {
                    errors.Add($"Uncertain usage cannot be safe: {change.Id}.");
                }

                if ((knownUse || decision.Usage == "used") && !citations.Any(citation => citation.Path.StartsWith(TestsRoot, StringComparison.Ordinal) && citation.Path.EndsWith(".cs", StringComparison.Ordinal)))
                {
                    errors.Add($"Safe consumed changes require test evidence: {change.Id}.");
                }
            }

            resolved.Add(decision with { Evidence = citations });
        }

        var summary = new ImpactCounts(resolved.Count(decision => decision.Verdict == "safe"), resolved.Count(decision => decision.Verdict == "needs-adaptation"),
            resolved.Count(decision => decision.Verdict == "needs-human-review"),
            resolved.Count(decision => CompleteAdoption(decision.Adoption) && decision.Adoption!.Outcome == "recommend-adoption"),
            resolved.Count(decision => CompleteAdoption(decision.Adoption) && decision.Adoption!.Outcome == "no-adoption-needed"),
            resolved.Count(decision => CompleteAdoption(decision.Adoption) && decision.Adoption!.Outcome == "needs-product-decision"));

        return new(1, errors.Count == 0, errors.Count == 0 && summary.NeedsAdaptation == 0 && summary.NeedsHumanReview == 0 && ChecksPassed(input.Checks), summary, errors, resolved);
    }

    private static ReviewCitation ResolveCitation(ReviewCitation citation, ImpactReviewInput input, string workspace)
    {
        if (citation is null || !input.Files.Any(file => file.Path == citation.Path))
        {
            throw new InvalidDataException($"Citation must identify an inventoried source file: {citation?.Path ?? "<null>"}. Review input and API models are not source evidence.");
        }

        var content = Normalize(File.ReadAllText(Resolve(workspace, citation.Path)));
        var lines = content.Split('\n');
        var quote = citation.Quote is null ? null : Normalize(citation.Quote);

        if (citation.StartLine.HasValue || citation.EndLine.HasValue)
        {
            if (citation.StartLine is not >= 1 || citation.EndLine is null || citation.EndLine < citation.StartLine || citation.EndLine > lines.Length)
            {
                throw new InvalidDataException($"Invalid source range: {citation.Path}.");
            }

            var actual = string.Join("\n", lines.Skip(citation.StartLine.Value - 1).Take(citation.EndLine.Value - citation.StartLine.Value + 1));

            if (string.IsNullOrWhiteSpace(actual) || (quote is not null && quote != actual))
            {
                throw new InvalidDataException($"Source range does not match quote or is empty: {citation.Path}.");
            }

            // Materialize exact evidence from the verified snapshot; the model only
            // needs to select the lines it inspected, rather than retype source text.
            return citation with { Quote = actual };
        }

        if (string.IsNullOrWhiteSpace(quote))
        {
            throw new InvalidDataException($"Citation requires a source range or a nonempty exact quote: {citation.Path}.");
        }

        var matches = new List<int>();
        var quotedLines = quote.Split('\n');

        for (var start = 0; start <= lines.Length - quotedLines.Length; start++)
        {
            if (lines.AsSpan(start, quotedLines.Length).SequenceEqual(quotedLines))
            {
                matches.Add(start);
            }
        }

        if (matches.Count != 1)
        {
            throw new InvalidDataException($"Quote must uniquely match complete source lines: {citation.Path}.");
        }

        return citation with { StartLine = matches[0] + 1, EndLine = matches[0] + quotedLines.Length, Quote = quote };
    }

    public static bool ChecksPassed(TestSummary checks)
    {
        bool Passed(string name) => checks.Checks.TryGetValue(name, out var status) && status == "success";

        return checks.Checks.Values.All(status => status == "success") && new[] { "tool", "compare", "build", "drift-tests", "collect", "manifest" }.All(Passed) &&
            (Passed("candidate-tests") || new[] { "contract-net8", "contract-net10", "boundary-net8", "boundary-net10" }.All(Passed));
    }

    public static ImpactValidation Invalid(params string[] errors) => new(1, false, false, new(0, 0, 0), errors, []);

    public static string Render(ImpactReviewInput input, ImpactValidation validation)
    {
        static string Cell(string value) => value.Replace("|", "\\|", StringComparison.Ordinal).ReplaceLineEndings(" ");

        var lines = new List<string> { "# Microsoft.Teams.Apps implementation impact review", "",
            $"Compared `{input.Comparison.FromVersion}` -> `{input.Comparison.ToVersion}`; review `{input.ReviewId}`.", "",
            $"Safe: **{validation.Summary.Safe}** | Needs adaptation: **{validation.Summary.NeedsAdaptation}** | Needs human review: **{validation.Summary.NeedsHumanReview}**.", "",
            $"Evidence validation: **{(validation.Valid ? "passed" : "FAILED")}**. Independent checks: **{(ChecksPassed(input.Checks) ? "passed" : "FAILED")}**. Gate: **{(validation.CanProceed ? "passed" : "requires attention")}**.", "",
            $"Feature adoption (nonblocking): recommend adoption **{validation.Summary.AdoptionRecommendations}** | no adoption needed **{validation.Summary.AdoptionNotNeeded}** | needs product decision **{validation.Summary.AdoptionNeedsProductDecision}**.", "",
            "| Change | API change type | Compatibility verdict | Usage | Impact on existing behavior |", "| --- | --- | --- | --- | --- |" };

        foreach (var decision in validation.Decisions)
        {
            var change = input.Comparison.Changes.Single(change => change.Id == decision.ChangeId);
            lines.Add($"| {decision.ChangeId} | {Cell(change.Kind)} | {decision.Verdict} | {decision.Usage} | {Cell(decision.Reason ?? "Missing reasoning")} |");

            foreach (var citation in decision.Evidence)
            {
                lines.Add($"| | | | Evidence | `{Cell(citation.Path)}:{citation.StartLine}-{citation.EndLine}` |");
            }
        }

        lines.AddRange(["", "## Feature adoption assessments (nonblocking)", "",
            "Assessment is required for every addition; adopting a feature is optional. Counts are per change. Related changes with the same outcome and reasoning are grouped below.", ""]);

        var additions = input.Comparison.Changes.Where(RequiresAdoptionAssessment).ToArray();
        var assessed = additions.Count(change => validation.Decisions.Any(decision => decision.ChangeId == change.Id &&
            CompleteAdoption(decision.Adoption)));
        lines.Add($"Additions assessed: **{assessed}/{additions.Length}**.");
        lines.Add("");

        var assessments = validation.Decisions.Where(decision => decision.Adoption is not null).GroupBy(decision => decision.Adoption!).ToArray();
        lines.AddRange(assessments.Length == 0 ? ["No adoption assessments recorded."] : assessments.Select(group =>
            $"- **{Cell(group.Key.Outcome ?? "Missing outcome")}** — {string.Join(", ", group.Select(decision => decision.ChangeId))}: {Cell(group.Key.Reason ?? "Missing reasoning")}"));
        lines.AddRange(["", "## Validation errors", ""]);
        lines.AddRange(validation.Errors.Count == 0 ? ["None."] : validation.Errors.Select(error => $"- {Cell(error)}"));
        lines.AddRange(["", "## Independent check outcomes", ""]);
        lines.AddRange(input.Checks.Checks.OrderBy(check => check.Key, StringComparer.Ordinal).Select(check => $"- {check.Key}: {check.Value}"));

        return string.Join("\n", lines) + "\n";
    }
}
