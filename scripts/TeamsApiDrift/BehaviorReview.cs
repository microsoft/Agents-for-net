using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace Microsoft.Agents.TeamsApiDrift;

public sealed record BehaviorCatalog(int SchemaVersion, IReadOnlyList<BehaviorDefinition> Behaviors);
public sealed record BehaviorDefinition(string Id, string Description, IReadOnlyList<string> SourceHints, IReadOnlyList<BehaviorCheck> Checks);
public sealed record BehaviorCheck(string Id, string Description);
public sealed record BehaviorSource(string Role, string Version, string Repository, string Commit);
public sealed record BehaviorInput(int SchemaVersion, string ReviewId, BehaviorCatalog Catalog, IReadOnlyList<BehaviorSource> Sources, IReadOnlyList<ReviewArtifact> Files);
public sealed record BehaviorDecision(string BehaviorId, string CheckId, string Outcome, string Reason,
    string? WireEffect, string? CompatibilityImpact, string? FollowUp, IReadOnlyList<ReviewCitation> Evidence, string? Title = null);
public sealed record BehaviorReview(int SchemaVersion, string ReviewId, IReadOnlyList<BehaviorDecision> Decisions);
public sealed record BehaviorValidation(int SchemaVersion, bool Valid, bool HasFindings, IReadOnlyList<string> Errors, IReadOnlyList<BehaviorDecision> Decisions);

public static class BehaviorReviewPipeline
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".cs", ".csproj", ".props", ".targets", ".md", ".json", ".slnx", ".xml" };

    private const string TeamsRepository = "https://github.com/microsoft/teams.net";

    private static string Identity(BehaviorInput input) => ImpactReviewPipeline.Hash(JsonSerializer.Serialize(input with { ReviewId = "" }, ToolJson.Options));

    internal static BehaviorSource ReadProvenance(Stream nuspec, string version, string role)
    {
        var document = XDocument.Load(nuspec);
        var metadata = document.Root?.Elements().SingleOrDefault(e => e.Name.LocalName == "metadata")
            ?? throw new InvalidDataException("Package metadata is missing.");

        string? Value(string name) => metadata.Elements().SingleOrDefault(e => e.Name.LocalName == name)?.Value;

        var repository = metadata.Elements().SingleOrDefault(e => e.Name.LocalName == "repository");
        var url = ((string?)repository?.Attribute("url"))?.TrimEnd('/');
        var commit = (string?)repository?.Attribute("commit");

        if (Value("id") != PackageConstants.PackageId || !NuGetVersion.TryParse(Value("version"), out var actual) ||
            !VersionComparer.VersionRelease.Equals(actual, NuGetVersion.Parse(version)) ||
            (url != TeamsRepository && url != TeamsRepository + ".git") ||
            commit is null || !Regex.IsMatch(commit, "\\A[0-9a-fA-F]{40}\\z"))
        {
            throw new InvalidDataException("Teams package identity or repository commit provenance is invalid.");
        }

        return new(role, version, TeamsRepository, commit.ToLowerInvariant());
    }

    private static async Task<(BehaviorSource Source, Dictionary<string, string> Files)> DownloadAsync(string version, string role)
    {
        Console.Error.WriteLine($"Preparing {role}: {PackageConstants.PackageId}@{version}.");

        var repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
        var resource = await repository.GetResourceAsync<FindPackageByIdResource>().ConfigureAwait(false)
            ?? throw new InvalidDataException("NuGet package resource is unavailable.");

        using var cache = new SourceCacheContext();
        using var package = new MemoryStream();

        if (!await resource.CopyNupkgToStreamAsync(PackageConstants.PackageId, NuGetVersion.Parse(version), package, cache, NullLogger.Instance, CancellationToken.None).ConfigureAwait(false))
        {
            throw new InvalidDataException($"Teams package {version} is unavailable.");
        }

        package.Position = 0;
        BehaviorSource source;

        using (var archive = new ZipArchive(package, ZipArchiveMode.Read, true))
        {
            using var nuspec = archive.Entries.Single(e => e.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase)).Open();
            source = ReadProvenance(nuspec, version, role);
        }

        Console.Error.WriteLine($"Resolved {role}: {source.Repository}@{source.Commit}.");

        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("TeamsApiDrift/1.0");
        using var stream = await client.GetStreamAsync($"https://codeload.github.com/microsoft/teams.net/zip/{source.Commit}").ConfigureAwait(false);
        using var bytes = new MemoryStream();
        await stream.CopyToAsync(bytes).ConfigureAwait(false);
        bytes.Position = 0;
        using var zip = new ZipArchive(bytes, ZipArchiveMode.Read);

        return (source, ReadArchive(zip));
    }

    internal static Dictionary<string, string> ReadArchive(ZipArchive zip)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var entry in zip.Entries)
        {
            var separator = entry.FullName.IndexOf('/');

            if (separator < 0 || entry.FullName.EndsWith('/') || !Supported(entry.FullName))
            {
                continue;
            }

            var path = entry.FullName[(separator + 1)..];

            // Validate archive paths before including them in the snapshot.
            if (path.Contains('\\') || path.Split('/').Any(p => p is ".." or "." or "") || Path.IsPathRooted(path) ||
                ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
            {
                throw new InvalidDataException("Invalid upstream source archive path.");
            }

            using var reader = new StreamReader(entry.Open());
            files.Add(path, reader.ReadToEnd());
        }

        return files;
    }

    private static bool Supported(string path) => SupportedExtensions.Contains(Path.GetExtension(path)) &&
        Path.GetFileName(path) is not ("AGENTS.md" or "CLAUDE.md" or "SKILL.md");

    public static async Task<BehaviorInput> PrepareAsync(string repositoryRoot, string workspace, BehaviorCatalog catalog, string to)
    {
        ValidateCatalog(catalog);

        if (Paths.IsContainedBy(Path.Combine(repositoryRoot, "src"), workspace) ||
            Paths.IsContainedBy(Path.Combine(repositoryRoot, ".git"), workspace))
        {
            throw new InvalidDataException("Behavior workspace must be outside source and Git metadata.");
        }

        var commit = ImpactReviewPipeline.Git(repositoryRoot, "rev-parse", "HEAD").Trim();
        Dictionary<string, string> agents;
        var archivePath = Path.Combine(Path.GetTempPath(), $"agents-behavior-{Guid.NewGuid():N}.zip");

        try
        {
            ImpactReviewPipeline.Git(repositoryRoot, "archive", "--format=zip", "--prefix=agents/", $"--output={archivePath}", commit);
            using var archive = ZipFile.OpenRead(archivePath);
            agents = ReadArchive(archive).Where(p => p.Key.StartsWith("src/", StringComparison.Ordinal) ||
                p.Key.StartsWith("docs/", StringComparison.Ordinal) || !p.Key.Contains('/')).ToDictionary(p => p.Key, p => p.Value);
        }
        finally
        {
            if (File.Exists(archivePath))
            {
                File.Delete(archivePath);
            }
        }

        var candidate = await DownloadAsync(to, "teams-candidate").ConfigureAwait(false);

        return Prepare(workspace, catalog, [
            (new BehaviorSource("agents", commit, ResolveAgentsRepository(repositoryRoot), commit), agents), candidate]);
    }

    private static string ResolveAgentsRepository(string repositoryRoot)
    {
        var remotes = ImpactReviewPipeline.Git(repositoryRoot, "remote", "-v");
        var match = Regex.Match(remotes, @"^origin\s+(?:https://github\.com/|git@github\.com:)([\w.-]+/[\w.-]+?)(?:\.git)?\s+\(fetch\)\r?$", RegexOptions.Multiline);

        return match.Success ? "https://github.com/" + match.Groups[1].Value : "Agents SDK checkout";
    }

    internal static BehaviorInput Prepare(string workspace, BehaviorCatalog catalog,
        IReadOnlyList<(BehaviorSource Source, Dictionary<string, string> Files)> sources)
    {
        ValidateCatalog(catalog);

        if (sources.Count != 2 || !sources.Select(s => s.Source.Role).Order().SequenceEqual(new[] { "agents", "teams-candidate" }.Order()))
        {
            throw new InvalidDataException("Agents and candidate Teams source roles are required.");
        }

        var destination = Path.GetFullPath(workspace);
        AgentContextBuilder.RejectSymlinkTraversal(Path.GetPathRoot(destination)!, destination, destination);

        if (Directory.Exists(destination) && Directory.EnumerateFileSystemEntries(destination).Any())
        {
            throw new InvalidDataException("Behavior workspace must be new or empty.");
        }

        Directory.CreateDirectory(destination);
        var inventory = new List<ReviewArtifact>();

        foreach (var (source, files) in sources)
        {
            if (!files.Keys.Any(p => p.EndsWith(".cs", StringComparison.Ordinal)))
            {
                throw new InvalidDataException($"Source is unavailable: {source.Role}.");
            }

            foreach (var (relative, original) in files.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                var path = $"{source.Role}/{relative}";
                var target = ImpactReviewPipeline.Resolve(destination, path);
                var content = AgentContextBuilder.Redact(original.TrimStart('\uFEFF').ReplaceLineEndings("\n"));
                content = Regex.Replace(content, """("[^"]*(?:secret|password|token|apiKey|connectionString|authorization)[^"]*"\s*:\s*")(?:\\.|[^"\\])*(")""", "$1[REDACTED]$2", RegexOptions.IgnoreCase);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.WriteAllText(target, content, new UTF8Encoding(false));
                inventory.Add(new(path, ImpactReviewPipeline.Hash(content)));
            }
        }

        var input = new BehaviorInput(1, "", catalog, sources.Select(s => s.Source).ToArray(), inventory);
        input = input with { ReviewId = Identity(input) };
        ToolJson.Write(Path.Combine(destination, "behavior-input.json"), input);

        return input;
    }

    private static void ValidateCatalog(BehaviorCatalog catalog)
    {
        if (catalog.SchemaVersion != 1 || catalog.Behaviors is null || catalog.Behaviors.Count == 0 ||
            catalog.Behaviors.Any(b => string.IsNullOrWhiteSpace(b.Id) || string.IsNullOrWhiteSpace(b.Description) || b.Checks is null || b.Checks.Count == 0 ||
                b.Checks.Any(c => string.IsNullOrWhiteSpace(c.Id) || string.IsNullOrWhiteSpace(c.Description)) || b.Checks.Select(c => c.Id).Distinct().Count() != b.Checks.Count) ||
            catalog.Behaviors.Select(b => b.Id).Distinct().Count() != catalog.Behaviors.Count)
        {
            throw new InvalidDataException("Invalid behavior catalog or duplicate check IDs.");
        }
    }

    public static BehaviorValidation Validate(BehaviorInput input, BehaviorReview review, string workspace)
    {
        var errors = new List<string>();
        var resolved = new List<BehaviorDecision>();
        ValidateCatalog(input.Catalog);

        if (input.Sources.Count != 2 || !input.Sources.Select(s => s.Role).Order().SequenceEqual(new[] { "agents", "teams-candidate" }.Order()) ||
            input.Files.Any(f => !f.Path.StartsWith("agents/", StringComparison.Ordinal) && !f.Path.StartsWith("teams-candidate/", StringComparison.Ordinal)))
        {
            errors.Add("Only Agents and candidate Teams source inventories are supported.");
        }

        if (input.SchemaVersion != 1 || review.SchemaVersion != 1 || input.ReviewId != Identity(input) || review.ReviewId != input.ReviewId)
        {
            errors.Add("Review identity or schema mismatch.");
        }

        var snapshotInput = ToolJson.Read<BehaviorInput>(Path.Combine(workspace, "behavior-input.json"));

        if (JsonSerializer.Serialize(snapshotInput, ToolJson.Options) != JsonSerializer.Serialize(input, ToolJson.Options))
        {
            errors.Add("Snapshot input changed.");
        }

        var inventory = input.Files.ToDictionary(f => f.Path, StringComparer.Ordinal);

        foreach (var file in input.Files)
        {
            var path = ImpactReviewPipeline.Resolve(workspace, file.Path);
            if (!File.Exists(path) || ImpactReviewPipeline.Hash(File.ReadAllText(path)) != file.ContentHash)
            {
                errors.Add($"Source changed: {file.Path}.");
            }
        }

        var required = input.Catalog.Behaviors.SelectMany(b => b.Checks.Select(c => (b.Id, c.Id))).ToHashSet();
        var seen = new HashSet<(string, string)>();

        foreach (var decision in review.Decisions ?? [])
        {
            var key = (decision.BehaviorId, decision.CheckId);
            if (!required.Contains(key) || !seen.Add(key))
            {
                errors.Add($"Unknown or duplicate check: {key}.");
            }

            if (decision.Outcome is not ("equivalent" or "different" or "inconclusive") || string.IsNullOrWhiteSpace(decision.Reason))
            {
                errors.Add($"Invalid decision: {key}.");
            }

            if (decision.Outcome == "different" && (string.IsNullOrWhiteSpace(decision.WireEffect) || string.IsNullOrWhiteSpace(decision.CompatibilityImpact) || string.IsNullOrWhiteSpace(decision.FollowUp)))
            {
                errors.Add($"Difference requires wire effect, compatibility impact, and follow-up: {key}.");
            }

            var citations = new List<ReviewCitation>();

            foreach (var citation in decision.Evidence ?? [])
            {
                if (!inventory.ContainsKey(citation.Path))
                {
                    errors.Add($"Unknown citation: {citation.Path}.");
                    continue;
                }

                var sourcePath = ImpactReviewPipeline.Resolve(workspace, citation.Path);

                if (!File.Exists(sourcePath))
                {
                    errors.Add($"Missing citation source: {citation.Path}.");
                    continue;
                }

                var lines = File.ReadAllText(sourcePath).Split('\n');

                if (citation.StartLine is not int start || citation.EndLine is not int end || start < 1 || end < start || end > lines.Length)
                {
                    errors.Add($"Invalid citation range: {citation.Path}.");
                    continue;
                }

                var quote = string.Join("\n", lines[(start - 1)..end]);

                if (string.IsNullOrWhiteSpace(quote) || (citation.Quote is not null && citation.Quote != quote))
                {
                    errors.Add($"Invalid citation quote: {citation.Path}.");
                }

                citations.Add(citation with { Quote = quote });
            }

            bool Cites(string role) => citations.Any(c => c.Path.StartsWith(role + "/", StringComparison.Ordinal));

            if (decision.Evidence is null || (decision.Outcome != "inconclusive" && (!Cites("agents") || !Cites("teams-candidate"))))
            {
                errors.Add($"Missing SDK evidence: {key}.");
            }

            resolved.Add(decision with { Evidence = citations });
        }

        if (!required.SetEquals(seen))
        {
            errors.Add("Every catalog check must have exactly one decision.");
        }

        return new(1, errors.Count == 0, resolved.Any(d => d.Outcome != "equivalent"), errors, resolved);
    }

    public static string Render(BehaviorInput input, BehaviorValidation validation)
    {
        var text = new StringBuilder("## Teams specific behavior analysis\n\nSource-based analysis; no traffic was captured. Findings are advisory.\n\n");
        text.AppendLine($"Evidence validation: **{(validation.Valid ? "passed" : "FAILED")}**. Review: `{input.ReviewId}`.\n");

        foreach (var source in input.Sources)
        {
            text.AppendLine($"- {source.Role}: `{source.Version}` at `{source.Commit}`");

            if (source.Repository.StartsWith("LOCAL TEST FIXTURE", StringComparison.Ordinal))
            {
                text.AppendLine($"  {source.Repository}. Patched snapshot references have no upstream permalink.");
            }
        }

        foreach (var error in validation.Errors)
        {
            text.AppendLine($"\nValidation error: {error}");
        }

        if (validation.Valid && !validation.HasFindings)
        {
            text.AppendLine("\nNo behavior differences or inconclusive results found.");
        }

        foreach (var decision in validation.Decisions.Where(decision => decision.Outcome != "equivalent"))
        {
            var title = string.IsNullOrWhiteSpace(decision.Title) ? $"{decision.BehaviorId} / {decision.CheckId}: {decision.Outcome}" : decision.Title;
            var summary = decision.Outcome == "different" ? decision.WireEffect ?? decision.Reason : decision.Reason;
            text.AppendLine($"\n### {title}\n\n{summary}");

            if (decision.CompatibilityImpact is not null)
            {
                text.AppendLine($"\n**Potential impact:** {decision.CompatibilityImpact}");
            }

            if (decision.FollowUp is not null)
            {
                text.AppendLine($"\n**Next step:** {decision.FollowUp}");
            }

            if (decision.Evidence.Count > 0)
            {
                text.AppendLine($"\n**Sources:** {string.Join(" · ", decision.Evidence.DistinctBy(c => (c.Path, c.StartLine, c.EndLine)).Select(c => RenderSource(input, c)))}");
            }
        }

        return text.ToString();
    }

    private static string RenderSource(BehaviorInput input, ReviewCitation citation)
    {
        var separator = citation.Path.IndexOf('/');

        if (separator < 1)
        {
            return $"Source snapshot: `{citation.Path}:{citation.StartLine}-{citation.EndLine}`";
        }

        var role = citation.Path[..separator];
        var path = citation.Path[(separator + 1)..];
        var source = input.Sources.FirstOrDefault(s => s.Role == role);
        var sdk = role == "agents" ? "Agents SDK" : "Teams SDK";
        var label = $"{sdk}: {Path.GetFileName(path)} ({citation.StartLine}–{citation.EndLine})";

        if (source is not null && Regex.IsMatch(source.Repository, @"\Ahttps://github\.com/[\w.-]+/[\w.-]+\z") &&
            Regex.IsMatch(source.Commit, @"\A[0-9a-fA-F]{40}\z"))
        {
            var escapedPath = string.Join("/", path.Split('/').Select(Uri.EscapeDataString));
            return $"[{label}]({source.Repository}/blob/{source.Commit}/{escapedPath}#L{citation.StartLine}-L{citation.EndLine})";
        }

        return $"{sdk} snapshot: `{path}:{citation.StartLine}-{citation.EndLine}`";
    }
}
