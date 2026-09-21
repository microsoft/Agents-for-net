using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Microsoft.Agents.TeamsApiDrift;

public static class MetadataValidator
{
    internal const string UsageStaleRule = "compat/teams-api-usage-manifest-stale";
    internal const string UsageReviewRule = "compat/teams-api-usage-review-missing";
    internal const string CapabilitiesStaleRule = "compat/teams-api-capabilities-stale";
    internal const string CapabilitiesReviewRule = "compat/teams-api-capabilities-review-missing";
    internal const string UsageReviewOutcome = "no-usage-metadata-change";
    internal const string CapabilitiesReviewOutcome = "no-capability-metadata-change";

    private const string ProjectFile = "src/libraries/Extensions/Microsoft.Agents.Extensions.MSTeams/Microsoft.Agents.Extensions.MSTeams.csproj";

    public static MetadataValidation Validate(
        UsageManifest manifest,
        CapabilityDocument capabilities,
        CollectedUsage collected,
        string declaredVersion,
        string repositoryRoot,
        string manifestPath,
        string capabilitiesPath,
        string? baseRef = null)
    {
        var root = Path.GetFullPath(repositoryRoot);
        var manifestRelativePath = RepositoryPath(root, manifestPath);
        var capabilitiesRelativePath = RepositoryPath(root, capabilitiesPath);
        var findings = new List<MetadataFinding>();

        ValidateManifest(manifest, collected, declaredVersion, root, manifestRelativePath, findings);
        ValidateCapabilities(manifest, capabilities, capabilitiesRelativePath, findings);
        ValidateReviewShape(manifest.SourceReview, UsageReviewOutcome, UsageStaleRule, manifestRelativePath, findings);
        ValidateReviewShape(capabilities.SourceReview, CapabilitiesReviewOutcome, CapabilitiesStaleRule, capabilitiesRelativePath, findings);

        var git = GitChangeSet.TryCreate(root, baseRef);
        if (git is not null)
        {
            ValidateChangeReviews(root, git, manifest, capabilities, manifestRelativePath, capabilitiesRelativePath, findings);
        }

        var ordered = findings
            .OrderBy(item => item.Path, StringComparer.Ordinal)
            .ThenBy(item => item.RuleId, StringComparer.Ordinal)
            .ThenBy(item => item.Message, StringComparer.Ordinal)
            .ToArray();
        return new MetadataValidation(1, ordered.Length == 0, ordered);
    }

    private static void ValidateManifest(
        UsageManifest manifest,
        CollectedUsage collected,
        string declaredVersion,
        string root,
        string manifestPath,
        List<MetadataFinding> findings)
    {
        if (manifest.SchemaVersion != 1)
        {
            Add(findings, UsageStaleRule, manifestPath, "Usage manifest must use schemaVersion 1.", "Set schemaVersion to 1.");
        }
        if (manifest.Package != PackageConstants.PackageId)
        {
            Add(findings, UsageStaleRule, manifestPath, $"Usage manifest must describe {PackageConstants.PackageId}.", $"Set package to {JsonSerializer.Serialize(PackageConstants.PackageId)}.");
        }
        if (manifest.DeclaredVersion != declaredVersion)
        {
            Add(findings, UsageStaleRule, manifestPath, $"Manifest version {manifest.DeclaredVersion} does not match Directory.Packages.props version {declaredVersion}.", $"Set declaredVersion to {JsonSerializer.Serialize(declaredVersion)}.");
        }
        if (Paths.Normalize(manifest.SourceRoot).TrimEnd('/') != Paths.Normalize(PackageConstants.SourceRoot))
        {
            Add(findings, UsageStaleRule, manifestPath, $"sourceRoot must be {JsonSerializer.Serialize(PackageConstants.SourceRoot)}.", "Restore the MSTeams source root.");
        }

        foreach (var duplicate in manifest.Usages.GroupBy(item => item.UpstreamSymbol, StringComparer.Ordinal).Where(group => group.Count() > 1))
        {
            Add(findings, UsageStaleRule, manifestPath, $"Usage manifest declares {duplicate.Key} more than once.", "Merge the duplicate usage entries.", duplicate.Key);
        }

        var manifestBySymbol = manifest.Usages
            .Where(item => !string.IsNullOrWhiteSpace(item.UpstreamSymbol))
            .GroupBy(item => item.UpstreamSymbol, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var collectedBySymbol = collected.Usages.ToDictionary(item => item.UpstreamSymbol, StringComparer.Ordinal);
        foreach (var actual in collected.Usages)
        {
            if (!manifestBySymbol.TryGetValue(actual.UpstreamSymbol, out var expected))
            {
                Add(findings, UsageStaleRule, manifestPath, $"Built extension usage is missing from the manifest: {actual.UpstreamSymbol}.", "Add the symbol and its affected source files to the usage manifest.", actual.UpstreamSymbol);
                continue;
            }

            if (expected.Members.Count > 0)
            {
                foreach (var member in actual.Members.Except(expected.Members, StringComparer.Ordinal))
                {
                    Add(findings, UsageStaleRule, manifestPath, $"Built extension member usage is missing from the manifest: {actual.UpstreamSymbol}.{member}.", "Add the member to the usage entry.", actual.UpstreamSymbol);
                }
                foreach (var member in expected.Members.Except(actual.Members, StringComparer.Ordinal))
                {
                    Add(findings, UsageStaleRule, manifestPath, $"Manifest member is stale; the built extension does not reference {actual.UpstreamSymbol}.{member}.", "Remove the stale member or restore its usage.", actual.UpstreamSymbol);
                }
            }
            if (!string.Equals(actual.Exposure, expected.Exposure, StringComparison.Ordinal))
            {
                Add(findings, UsageStaleRule, manifestPath, $"{actual.UpstreamSymbol} has {actual.Exposure} exposure in the built extension but the manifest records {expected.Exposure}.", $"Set exposure to {JsonSerializer.Serialize(actual.Exposure)}.", actual.UpstreamSymbol);
            }
        }

        foreach (var expected in manifest.Usages)
        {
            if (string.IsNullOrWhiteSpace(expected.UpstreamSymbol))
            {
                Add(findings, UsageStaleRule, manifestPath, "Every usage must include a non-empty upstreamSymbol.", "Repair or remove the malformed usage entry.");
                continue;
            }
            if (!collectedBySymbol.ContainsKey(expected.UpstreamSymbol))
            {
                Add(findings, UsageStaleRule, manifestPath, $"Manifest usage is stale; the built extension does not reference {expected.UpstreamSymbol}.", "Remove the stale usage entry or restore its usage.", expected.UpstreamSymbol);
            }
            if (expected.Files.Count == 0)
            {
                Add(findings, UsageStaleRule, manifestPath, $"Usage {expected.UpstreamSymbol} must identify at least one affected source file.", "Add the affected MSTeams source path.", expected.UpstreamSymbol);
            }
            foreach (var file in expected.Files)
            {
                ValidateSourceFile(manifest, collected, root, manifestPath, expected.UpstreamSymbol, file, findings);
            }
        }
    }

    private static void ValidateSourceFile(
        UsageManifest manifest,
        CollectedUsage collected,
        string root,
        string manifestPath,
        string symbol,
        string file,
        List<MetadataFinding> findings)
    {
        var sourceRoot = Path.GetFullPath(Path.Combine(root, manifest.SourceRoot));
        var fullPath = Path.GetFullPath(Path.Combine(root, file));
        if (!Paths.IsContainedBy(root, sourceRoot) || !Paths.IsContainedBy(sourceRoot, fullPath) || !File.Exists(fullPath))
        {
            Add(findings, UsageStaleRule, manifestPath, $"Usage {symbol} references missing or unsafe source file {JsonSerializer.Serialize(file)}.", "Replace it with an existing file under the MSTeams source root.", symbol);
            return;
        }

        var collectedFiles = collected.SourceFiles ?? [];
        if (collectedFiles.Count > 0 && !collectedFiles.Any(document => Paths.Normalize(document).EndsWith(Paths.Normalize(file), StringComparison.OrdinalIgnoreCase)))
        {
            Add(findings, UsageStaleRule, manifestPath, $"Usage {symbol} source file is absent from the portable PDB: {file}.", "Use a compiled MSTeams source file or correct the manifest path.", symbol);
        }
    }

    private static void ValidateCapabilities(
        UsageManifest manifest,
        CapabilityDocument document,
        string path,
        List<MetadataFinding> findings)
    {
        if (document.SchemaVersion != 1)
        {
            Add(findings, CapabilitiesStaleRule, path, "Capabilities metadata must use schemaVersion 1.", "Set schemaVersion to 1.");
        }
        if (document.Package != PackageConstants.PackageId)
        {
            Add(findings, CapabilitiesStaleRule, path, $"Capabilities metadata must describe {PackageConstants.PackageId}.", $"Set package to {JsonSerializer.Serialize(PackageConstants.PackageId)}.");
        }
        if (document.Capabilities.Count == 0)
        {
            Add(findings, CapabilitiesStaleRule, path, "Capabilities metadata must contain at least one capability.", "Restore the capability mappings.");
        }
        foreach (var (name, capability) in document.Capabilities)
        {
            if (string.IsNullOrWhiteSpace(name) || capability.Owners.Count == 0 || string.IsNullOrWhiteSpace(capability.AdoptionPolicy) ||
                capability.UpstreamNamespaces.Count + capability.UpstreamTypes.Count == 0)
            {
                Add(findings, CapabilitiesStaleRule, path, $"Capability {name} must include owners, an adoptionPolicy, and at least one upstream namespace or type.", "Repair the malformed capability mapping.", name);
            }
        }
        foreach (var usage in manifest.Usages.Where(item => !string.IsNullOrWhiteSpace(item.UpstreamSymbol)))
        {
            if (FindingClassifier.MatchCapability(usage.UpstreamSymbol, document).Value is null)
            {
                Add(findings, CapabilitiesStaleRule, path, $"No capability maps usage symbol {usage.UpstreamSymbol}.", "Add an exact upstreamTypes entry or a containing upstreamNamespaces entry.", usage.UpstreamSymbol);
            }
        }
    }

    private static void ValidateReviewShape(
        SourceReview? review,
        string expectedOutcome,
        string rule,
        string path,
        List<MetadataFinding> findings)
    {
        if (review is null) return;
        if (review.Outcome != expectedOutcome || string.IsNullOrWhiteSpace(review.Reason))
        {
            Add(findings, rule, path, $"sourceReview must use outcome {JsonSerializer.Serialize(expectedOutcome)} and include a non-empty reason.", "Correct sourceReview or remove it when the document contains a substantive metadata update.");
        }
    }

    private static void ValidateChangeReviews(
        string root,
        GitChangeSet git,
        UsageManifest currentManifest,
        CapabilityDocument currentCapabilities,
        string manifestPath,
        string capabilitiesPath,
        List<MetadataFinding> findings)
    {
        var baseManifest = git.ReadJson<UsageManifest>(manifestPath);
        var baseCapabilities = git.ReadJson<CapabilityDocument>(capabilitiesPath);
        if (baseManifest is null || baseCapabilities is null) return;

        var currentUsageFiles = currentManifest.Usages.SelectMany(item => item.Files).Select(Paths.Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var baseUsageFiles = baseManifest.Usages.SelectMany(item => item.Files).Select(Paths.Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sourcePrefix = Paths.Normalize(PackageConstants.SourceRoot).TrimEnd('/') + "/";
        var changedSourceFiles = git.ChangedFiles
            .Where(file => file.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase) && file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var usageRelevant = changedSourceFiles.Where(file =>
        {
            var newFile = git.ReadBaseFile(file) is null && File.Exists(Path.Combine(root, file));
            return newFile || currentUsageFiles.Contains(file) || baseUsageFiles.Contains(file) ||
                ContainsTeamsReference(git.ReadBaseFile(file)) || ContainsTeamsReference(ReadWorkingFile(root, file));
        }).ToList();
        if (git.ChangedFiles.Contains(ProjectFile)) usageRelevant.Add(ProjectFile);
        usageRelevant = usageRelevant.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList();

        if (usageRelevant.Count > 0)
        {
            var fresh = git.MetadataChangeIsFresh(usageRelevant, manifestPath);
            var substantiveUpdate = !SemanticEqual(baseManifest.Usages, currentManifest.Usages);
            var explicitReview = ReviewChanged(baseManifest.SourceReview, currentManifest.SourceReview, UsageReviewOutcome);
            if (!fresh || (!substantiveUpdate && !explicitReview))
            {
                Add(findings, UsageReviewRule, manifestPath, $"{usageRelevant.Count} Teams API usage-related source file(s) changed without a fresh usage-manifest update or non-impact review.", "Update usages, or update sourceReview with outcome \"no-usage-metadata-change\" and a specific reason.", string.Join(", ", usageRelevant.Take(4)));
            }
        }

        var baseSymbols = baseManifest.Usages.Select(item => item.UpstreamSymbol).ToHashSet(StringComparer.Ordinal);
        var currentSymbols = currentManifest.Usages.Select(item => item.UpstreamSymbol).ToHashSet(StringComparer.Ordinal);
        var affectedSymbols = baseSymbols.SymmetricExcept(currentSymbols).Order(StringComparer.Ordinal).ToArray();
        if (affectedSymbols.Length > 0 && usageRelevant.Count > 0)
        {
            var fresh = git.MetadataChangeIsFresh(usageRelevant, capabilitiesPath);
            var targetedUpdate = fresh && affectedSymbols.All(symbol =>
                !string.Equals(CapabilityFingerprint(baseCapabilities, symbol), CapabilityFingerprint(currentCapabilities, symbol), StringComparison.Ordinal));
            var explicitReview = ReviewChanged(baseCapabilities.SourceReview, currentCapabilities.SourceReview, CapabilitiesReviewOutcome);
            if (!targetedUpdate && !(fresh && explicitReview))
            {
                Add(findings, CapabilitiesReviewRule, capabilitiesPath, $"Teams API usage symbols changed without a targeted capability update or fresh non-impact review: {string.Join(", ", affectedSymbols)}.", "Update the affected capability mapping, or update sourceReview with outcome \"no-capability-metadata-change\" and a specific reason.", string.Join(", ", affectedSymbols.Take(4)));
            }
        }
    }

    private static string? CapabilityFingerprint(CapabilityDocument document, string symbol)
    {
        var match = FindingClassifier.MatchCapability(symbol, document);
        return match.Value is null ? null : $"{match.Key}:{JsonSerializer.Serialize(match.Value, ToolJson.Options)}";
    }

    private static bool ReviewChanged(SourceReview? before, SourceReview? after, string outcome)
        => !SemanticEqual(before, after) && after?.Outcome == outcome && !string.IsNullOrWhiteSpace(after.Reason);

    private static bool SemanticEqual<T>(T before, T after)
        => string.Equals(JsonSerializer.Serialize(before, ToolJson.Options), JsonSerializer.Serialize(after, ToolJson.Options), StringComparison.Ordinal);

    private static bool ContainsTeamsReference(string? text)
        => text?.Contains(PackageConstants.PackageId, StringComparison.Ordinal) == true;

    private static string? ReadWorkingFile(string root, string relativePath)
    {
        var path = Path.Combine(root, relativePath);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    private static string RepositoryPath(string root, string path)
    {
        var fullPath = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(root, path));
        if (!Paths.IsContainedBy(root, fullPath)) throw new InvalidDataException($"Metadata path escapes the repository: {path}.");
        return Paths.Normalize(Path.GetRelativePath(root, fullPath));
    }

    private static void Add(List<MetadataFinding> findings, string rule, string path, string message, string fix, string? subject = null)
        => findings.Add(new MetadataFinding(rule, Paths.Normalize(path), message, fix, subject));
}

internal sealed class GitChangeSet
{
    private readonly GitRepository _repository;

    private GitChangeSet(GitRepository repository, string @base, HashSet<string> workingFiles, HashSet<string> changedFiles)
    {
        _repository = repository;
        Base = @base;
        WorkingFiles = workingFiles;
        ChangedFiles = changedFiles;
    }

    public string Base { get; }
    public HashSet<string> WorkingFiles { get; }
    public HashSet<string> ChangedFiles { get; }

    public static GitChangeSet? TryCreate(string root, string? explicitBaseRef)
    {
        var repository = new GitRepository(root);
        if (!repository.Succeeds("rev-parse", "--is-inside-work-tree"))
        {
            if (!string.IsNullOrWhiteSpace(explicitBaseRef)) throw new InvalidOperationException($"Unable to resolve Git base ref {explicitBaseRef}.");
            return null;
        }

        var baseRef = ResolveBaseRef(repository, explicitBaseRef);
        var mergeBase = repository.Text("merge-base", "HEAD", baseRef)?.Trim();
        if (string.IsNullOrWhiteSpace(mergeBase))
        {
            if (!string.IsNullOrWhiteSpace(explicitBaseRef)) throw new InvalidOperationException($"Unable to resolve Git base ref {explicitBaseRef}.");
            return null;
        }

        var committed = repository.NullList("diff", "--name-only", "--no-renames", "-z", mergeBase, "HEAD");
        var working = repository.NullList("diff", "--name-only", "--no-renames", "-z", "HEAD");
        working.UnionWith(repository.NullList("ls-files", "--others", "--exclude-standard", "-z"));
        committed.UnionWith(working);
        return new GitChangeSet(repository, mergeBase, working, committed);
    }

    public string? ReadBaseFile(string path) => _repository.Text("show", $"{Base}:{Paths.Normalize(path)}");

    public T? ReadJson<T>(string path) where T : class
    {
        var text = ReadBaseFile(path);
        if (text is null) return null;
        try
        {
            return JsonSerializer.Deserialize<T>(text, ToolJson.Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public bool MetadataChangeIsFresh(IReadOnlyCollection<string> sourceFiles, string metadataFile)
    {
        var normalizedSources = sourceFiles.Select(Paths.Normalize).ToArray();
        var normalizedMetadata = Paths.Normalize(metadataFile);
        if (normalizedSources.Any(WorkingFiles.Contains)) return WorkingFiles.Contains(normalizedMetadata);
        if (WorkingFiles.Contains(normalizedMetadata)) return true;

        var sourceCommit = LatestCommit(normalizedSources);
        var metadataCommit = LatestCommit([normalizedMetadata]);
        return sourceCommit is not null && metadataCommit is not null &&
            (sourceCommit == metadataCommit || _repository.Succeeds("merge-base", "--is-ancestor", sourceCommit, metadataCommit));
    }

    private string? LatestCommit(IReadOnlyCollection<string> files)
    {
        var arguments = new List<string> { "log", "-1", "--format=%H", $"{Base}..HEAD", "--" };
        arguments.AddRange(files);
        var commit = _repository.Text(arguments.ToArray())?.Trim();
        return string.IsNullOrWhiteSpace(commit) ? null : commit;
    }

    private static string ResolveBaseRef(GitRepository repository, string? explicitBaseRef)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(explicitBaseRef))
        {
            candidates.Add(explicitBaseRef);
        }
        else
        {
            var githubBase = Environment.GetEnvironmentVariable("GITHUB_BASE_REF");
            if (!string.IsNullOrWhiteSpace(githubBase)) candidates.AddRange([$"origin/{githubBase}", githubBase]);
            var azureBase = Environment.GetEnvironmentVariable("SYSTEM_PULLREQUEST_TARGETBRANCH");
            if (!string.IsNullOrWhiteSpace(azureBase))
            {
                var branch = azureBase.Replace("refs/heads/", string.Empty, StringComparison.Ordinal);
                candidates.AddRange([$"origin/{branch}", branch, azureBase]);
            }
            candidates.AddRange(["origin/main", "upstream/main", "main", "HEAD"]);
        }

        var resolved = candidates.FirstOrDefault(candidate => repository.Succeeds("rev-parse", "--verify", $"{candidate}^{{commit}}"));
        if (resolved is null && !string.IsNullOrWhiteSpace(explicitBaseRef)) throw new InvalidOperationException($"Unable to resolve Git base ref {explicitBaseRef}.");
        return resolved ?? "HEAD";
    }
}

internal sealed class GitRepository(string root)
{
    public string? Text(params string[] arguments)
    {
        var result = Run(arguments);
        return result.ExitCode == 0 ? result.Output : null;
    }

    public bool Succeeds(params string[] arguments) => Run(arguments).ExitCode == 0;

    public HashSet<string> NullList(params string[] arguments)
        => Text(arguments)?.Split('\0', StringSplitOptions.RemoveEmptyEntries).Select(Paths.Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new(StringComparer.OrdinalIgnoreCase);

    private (int ExitCode, string Output) Run(IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start Git.");
        var errorTask = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        _ = errorTask.GetAwaiter().GetResult();
        return (process.ExitCode, output);
    }
}

internal static class SetExtensions
{
    public static HashSet<T> SymmetricExcept<T>(this IReadOnlySet<T> left, IReadOnlySet<T> right)
    {
        var result = new HashSet<T>(left, left is HashSet<T> set ? set.Comparer : EqualityComparer<T>.Default);
        result.SymmetricExceptWith(right);
        return result;
    }
}
