namespace Microsoft.Agents.TeamsApiDrift;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return 2;
            }

            var options = new Arguments(args.Skip(1));
            return args[0] switch
            {
                "write-agent-metadata" => WriteAgentMetadata(options),
                "extract-agent-response" => ExtractAgentResponse(options),
                "resolve-version" => ResolveVersion(options),
                "resolve-comparison" => await ResolveComparisonAsync(options).ConfigureAwait(false),
                "compare" => await CompareAsync(options).ConfigureAwait(false),
                "collect-usage" => CollectUsage(options),
                "validate-usage" => ValidateUsage(options),
                "validate-metadata" => ValidateMetadata(options),
                "write-test-summary" => WriteTestSummary(options),
                "prepare-impact-review" => PrepareImpactReview(options),
                "validate-impact-review" => ValidateImpactReview(options),
                _ => throw new ArgumentException($"Unknown command: {args[0]}")
            };
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"TeamsApiDrift: {exception.Message}");
            return 2;
        }
    }

    private static int WriteAgentMetadata(Arguments options)
    {
        ReviewAgentOutput.WriteMetadata(options.Required("--output"), options.Required("--model"), options.Required("--cli-version"), Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY"));
        return 0;
    }

    private static int ExtractAgentResponse(Arguments options)
    {
        ReviewAgentOutput.WriteResponse(options.Required("--input"), options.Required("--output"), Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY"));
        return 0;
    }

    private static int ResolveVersion(Arguments options)
    {
        using TextReader reader = options.Optional("--props") is "-" or null
            ? Console.In
            : File.OpenText(options.Required("--props"));
        Console.WriteLine(VersionResolver.Resolve(reader, options.Optional("--package") ?? PackageConstants.PackageId));
        return 0;
    }

    private static async Task<int> ResolveComparisonAsync(Arguments options)
    {
        var from = options.Required("--from");
        var to = options.Optional("--to") ?? await new PackageApiService(options.Many("--source"), options.Optional("--config-file"))
            .GetLatestStableVersionAsync().ConfigureAwait(false);
        var versions = VersionResolver.Compare(from, to);

        ToolJson.Write(options.Required("--output"), versions);

        Console.Error.WriteLine($"Summary: Resolved {PackageConstants.PackageId} {versions.FromVersion} -> {versions.ToVersion}; " +
            (versions.SameVersion ? "versions match, downstream jobs will be skipped." : "versions differ, deterministic checks and implementation review are required."));

        return 0;
    }

    private static async Task<int> CompareAsync(Arguments options)
    {
        var service = new PackageApiService(options.Many("--source"), options.Optional("--config-file"));
        var from = options.Required("--from");
        var to = options.Optional("--to") ?? await service.GetLatestStableVersionAsync().ConfigureAwait(false);
        var output = Path.GetFullPath(options.Optional("--output") ?? PackageConstants.ArtifactDirectory);
        var before = await service.ExtractAsync(from).ConfigureAwait(false);
        var after = await service.ExtractAsync(to).ConfigureAwait(false);
        var comparison = ApiComparer.Compare(before, after);

        ToolJson.Write(Path.Combine(output, "microsoft-teams-apps-before.api.json"), before);
        ToolJson.Write(Path.Combine(output, "microsoft-teams-apps-after.api.json"), after);
        ToolJson.Write(Path.Combine(output, "raw-api-diff.json"), comparison);
        
        Console.Error.WriteLine($"Summary: Compared {PackageConstants.PackageId} {from} -> {to}; {comparison.Changes.Count} API changes.");

        foreach (var group in comparison.Changes.GroupBy(change => change.Kind).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            Console.Error.WriteLine($"  {group.Key}: {group.Count()}");
        }

        Console.WriteLine(to);

        return 0;
    }

    private static int CollectUsage(Arguments options)
    {
        var assemblies = options.Many("--assembly");
        
        if (assemblies.Count == 0)
        {
            throw new ArgumentException("At least one --assembly is required.");
        }

        var result = CollectUsage(assemblies);

        ToolJson.Write(ToolJson.OutputFile(options.Required("--output"), "collected-usage.json", ".json"), result);

        Console.Error.WriteLine($"Summary: Collected {result.Usages.Count} dependency symbols and {result.Usages.Sum(usage => usage.Members.Count)} referenced members from {assemblies.Count} assemblies.");

        return 0;
    }

    private static CollectedUsage CollectUsage(IEnumerable<string> assemblies)
    {
        var collected = assemblies.Select(AssemblyUsageCollector.Collect).ToArray();
        var usages = collected.SelectMany(item => item.Usages)
            .GroupBy(item => item.UpstreamSymbol, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new CollectedUsageEntry(
                group.Key,
                group.SelectMany(item => item.Members).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                group.Any(item => item.Exposure == "publicly-exposed") ? "publicly-exposed" : "internal-only"))
            .ToArray();
        var sourceFiles = collected.SelectMany(item => item.SourceFiles ?? []).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray();

        return new CollectedUsage(1, PackageConstants.PackageId, string.Join(",", collected.Select(item => item.Assembly)), usages, sourceFiles);
    }

    private static int ValidateUsage(Arguments options)
    {
        var result = UsageValidator.Validate(
            ToolJson.Read<UsageManifest>(options.Required("--manifest")),
            ToolJson.Read<CollectedUsage>(options.Required("--collected")),
            ToolJson.Read<ApiModel>(options.Required("--api-model")),
            options.Optional("--repository-root") ?? Directory.GetCurrentDirectory());
        ToolJson.Write(ToolJson.OutputFile(options.Required("--output"), "usage-validation.json", ".json"), result);

        Console.Error.WriteLine($"Summary: Usage manifest validation {(result.Valid ? "passed" : "FAILED")}; {result.Errors.Count} errors, {result.Warnings.Count} warnings.");

        foreach (var error in result.Errors)
        {
            Console.Error.WriteLine($"  Error: {error}");
        }

        foreach (var warning in result.Warnings)
        {
            Console.Error.WriteLine($"  Warning: {warning}");
        }

        return result.Valid ? 0 : 1;
    }

    private static int ValidateMetadata(Arguments options)
    {
        var assemblies = options.Many("--assembly");

        if (assemblies.Count == 0)
        {
            throw new ArgumentException("At least one --assembly is required.");
        }

        var propsPath = options.Required("--props");

        using var props = File.OpenText(propsPath);

        var result = MetadataValidator.Validate(
            ToolJson.Read<UsageManifest>(options.Required("--manifest")),
            ToolJson.Read<CapabilityDocument>(options.Required("--capabilities")),
            CollectUsage(assemblies),
            VersionResolver.Resolve(props),
            options.Optional("--repository-root") ?? Directory.GetCurrentDirectory(),
            options.Required("--manifest"),
            options.Required("--capabilities"),
            options.Optional("--base-ref"));

        foreach (var finding in result.Findings)
        {
            Console.Error.WriteLine($"TeamsApiDrift: [{finding.RuleId}] {finding.Path}: {finding.Message} Fix: {finding.Fix}");
        }

        return result.Valid ? 0 : 1;
    }

    private static int WriteTestSummary(Arguments options)
    {
        var checks = options.Many("--check").Select(value => value.Split('=', 2))
            .ToDictionary(parts => parts[0], parts => parts.Length == 2 ? parts[1] : "unknown", StringComparer.Ordinal);
        ToolJson.Write(ToolJson.OutputFile(options.Required("--output"), "test-summary.json", ".json"), new TestSummary(1, checks));

        Console.Error.WriteLine($"Summary: Recorded {checks.Count} independent check outcomes.");

        foreach (var check in checks.OrderBy(check => check.Key, StringComparer.Ordinal))
        {
            Console.Error.WriteLine($"  {check.Key}: {check.Value}");
        }

        return 0;
    }

    private static int PrepareImpactReview(Arguments options)
    {
        var collectedPath = options.Optional("--collected");
        var input = ImpactReviewPipeline.Prepare(
            options.Optional("--repository-root") ?? Directory.GetCurrentDirectory(), options.Required("--workspace"),
            ToolJson.Read<ApiComparison>(options.Required("--comparison")),
            ToolJson.Read<UsageManifest>(options.Required("--manifest")),
            ToolJson.Read<CapabilityDocument>(options.Required("--capabilities")),
            collectedPath is not null && File.Exists(collectedPath) ? ToolJson.Read<CollectedUsage>(collectedPath) : null,
            ToolJson.Read<TestSummary>(options.Required("--test-summary")),
            ToolJson.Read<ApiModel>(options.Required("--before-api")), ToolJson.Read<ApiModel>(options.Required("--after-api")));
        ToolJson.Write(options.Required("--output"), input);
        // No package changes still produces a validated empty review without an AI call.
        if (input.Comparison.Changes.Count == 0 || options.HasFlag("--defer"))
        {
            var decisions = input.Comparison.Changes.Select(change => new ImpactDecision(change.Id, "needs-human-review", "uncertain",
                "Automated repository review is unavailable for fork pull requests; a maintainer must review this candidate.", [],
                ImpactReviewPipeline.RequiresAdoptionAssessment(change) ? new("needs-product-decision",
                    "Automated adoption assessment is unavailable for fork pull requests; a maintainer must evaluate this addition.") : null)).ToArray();
            ToolJson.Write(options.Required("--empty-review"), new ImpactReview(1, input.ReviewId, input.Comparison.FromVersion, input.Comparison.ToVersion, decisions));
        }

        Console.Error.WriteLine($"Summary: Prepared implementation review of {input.Comparison.Changes.Count} API changes; {input.Files.Count} full source/test/document files; commit {input.Commit}.");

        foreach (var group in input.Comparison.Changes.GroupBy(change => change.Kind).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            Console.Error.WriteLine($"  {group.Key}: {group.Count()}");
        }

        Console.Error.WriteLine($"  Source navigation: {input.CollectedUsage?.SourceFiles?.Count ?? 0} inventoried relative PDB paths; {input.UnmappedSourceFileCount} unmapped hints omitted.");
        Console.Error.WriteLine($"  Review identity: {input.ReviewId}; snapshot: {options.Required("--workspace")}");
        Console.WriteLine(input.Comparison.Changes.Count);

        return 0;
    }

    private static int ValidateImpactReview(Arguments options)
    {
        var input = ToolJson.Read<ImpactReviewInput>(options.Required("--input"));
        ImpactValidation validation;
        try
        {
            var review = ImpactReviewPipeline.Parse(File.ReadAllText(options.Required("--review")));
            validation = ImpactReviewPipeline.Validate(review, input, options.Required("--workspace"),
                options.Optional("--repository-root") ?? Directory.GetCurrentDirectory());
        }
        catch (Exception exception) when (exception is System.Text.Json.JsonException or InvalidDataException or IOException or ArgumentException)
        {
            validation = ImpactReviewPipeline.Invalid(exception.Message);
        }
        ToolJson.Write(options.Required("--output"), validation);

        var reportPath = options.Required("--report");

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);

        File.WriteAllText(reportPath, ImpactReviewPipeline.Render(input, validation));

        Console.Error.WriteLine($"Summary: Implementation review validation {(validation.Valid ? "passed" : "FAILED")}; gate {(validation.CanProceed ? "passed" : "requires attention")}.");
        Console.Error.WriteLine($"  Safe: {validation.Summary.Safe}; needs adaptation: {validation.Summary.NeedsAdaptation}; needs human review: {validation.Summary.NeedsHumanReview}; recommend adoption: {validation.Summary.AdoptionRecommendations}; no adoption needed: {validation.Summary.AdoptionNotNeeded}; needs product decision: {validation.Summary.AdoptionNeedsProductDecision} (nonblocking); errors: {validation.Errors.Count}.");

        foreach (var decision in validation.Decisions)
        {
            Console.Error.WriteLine($"  {decision.ChangeId}: {decision.Verdict} ({decision.Usage}) — {decision.Reason}");

            if (decision.Adoption is { } adoption)
            {
                Console.Error.WriteLine($"    Feature adoption (nonblocking): {adoption.Outcome} — {adoption.Reason}");
            }
        }

        foreach (var error in validation.Errors)
        {
            Console.Error.WriteLine($"  Error: {error}");
        }

        Console.Error.WriteLine($"  Review report: {reportPath}");

        return validation.Valid ? 0 : 1;
    }

    private static void PrintUsage() => Console.Error.WriteLine(
        "Commands: write-agent-metadata, extract-agent-response, resolve-version, resolve-comparison, compare, collect-usage, validate-usage, validate-metadata, write-test-summary, prepare-impact-review, validate-impact-review");
}
