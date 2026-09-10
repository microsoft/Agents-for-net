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
                "resolve-version" => ResolveVersion(options),
                "compare" => await CompareAsync(options).ConfigureAwait(false),
                "collect-usage" => CollectUsage(options),
                "validate-usage" => ValidateUsage(options),
                "validate-metadata" => ValidateMetadata(options),
                "classify" => Classify(options),
                "write-test-summary" => WriteTestSummary(options),
                "render-report" => RenderReport(options),
                "prepare-agent-context" => PrepareAgentContext(options),
                "validate-agent-report" => ValidateAgentReport(options),
                _ => throw new ArgumentException($"Unknown command: {args[0]}")
            };
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"TeamsApiDrift: {exception.Message}");
            return 2;
        }
    }

    private static int ResolveVersion(Arguments options)
    {
        using TextReader reader = options.Optional("--props") is "-" or null
            ? Console.In
            : File.OpenText(options.Required("--props"));
        Console.WriteLine(VersionResolver.Resolve(reader, options.Optional("--package") ?? PackageConstants.PackageId));
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
        Console.WriteLine(to);
        return 0;
    }

    private static int CollectUsage(Arguments options)
    {
        var assemblies = options.Many("--assembly");
        if (assemblies.Count == 0) throw new ArgumentException("At least one --assembly is required.");
        var result = CollectUsage(assemblies);
        ToolJson.Write(ToolJson.OutputFile(options.Required("--output"), "collected-usage.json", ".json"), result);
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
        return result.Valid ? 0 : 1;
    }

    private static int ValidateMetadata(Arguments options)
    {
        var assemblies = options.Many("--assembly");
        if (assemblies.Count == 0) throw new ArgumentException("At least one --assembly is required.");
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

    private static int Classify(Arguments options)
    {
        var result = FindingClassifier.Classify(
            ToolJson.Read<ApiComparison>(options.Required("--comparison")),
            ToolJson.Read<UsageManifest>(options.Required("--manifest")),
            ToolJson.Read<CapabilityDocument>(options.Required("--capabilities")));
        ToolJson.Write(ToolJson.OutputFile(options.Required("--output"), "findings.json", ".json"), result);
        return options.HasFlag("--fail-on-drift") && result.Summary.Blocking + result.Summary.Required > 0 ? 1 : 0;
    }

    private static int WriteTestSummary(Arguments options)
    {
        var checks = options.Many("--check").Select(value => value.Split('=', 2))
            .ToDictionary(parts => parts[0], parts => parts.Length == 2 ? parts[1] : "unknown", StringComparer.Ordinal);
        ToolJson.Write(ToolJson.OutputFile(options.Required("--output"), "test-summary.json", ".json"), new TestSummary(1, checks));
        return 0;
    }

    private static int RenderReport(Arguments options)
    {
        var summaryPath = options.Optional("--test-summary");
        var markdown = DeterministicReportRenderer.Render(
            ToolJson.Read<FindingsResult>(options.Required("--findings")),
            summaryPath is null ? null : ToolJson.Read<TestSummary>(summaryPath));
        var output = ToolJson.OutputFile(options.Required("--output"), "deterministic-report.md", ".md");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllText(output, markdown);
        return 0;
    }

    private static int PrepareAgentContext(Arguments options)
    {
        var summaryPath = options.Optional("--test-summary");
        var context = AgentContextBuilder.Build(
            ToolJson.Read<FindingsResult>(options.Required("--findings")),
            ToolJson.Read<UsageManifest>(options.Required("--manifest")),
            ToolJson.Read<CapabilityDocument>(options.Required("--capabilities")),
            File.ReadAllText(options.Required("--deterministic-report")),
            summaryPath is null ? null : ToolJson.Read<TestSummary>(summaryPath),
            options.Optional("--repository-root") ?? Directory.GetCurrentDirectory());
        ToolJson.Write(ToolJson.OutputFile(options.Required("--output"), "agent-context.json", ".json"), context);
        return 0;
    }

    private static int ValidateAgentReport(Arguments options)
    {
        var validation = AgentReportValidator.Validate(
            File.ReadAllText(options.Required("--report")),
            ToolJson.Read<FindingsResult>(options.Required("--findings")));
        var output = ToolJson.OutputFile(options.Required("--output"), "agent-report-validation.json", ".json");
        ToolJson.Write(output, validation);
        if (!validation.Valid)
        {
            Console.Error.WriteLine($"TeamsApiDrift: Agent report validation failed. Details were written to '{output}'.");
            foreach (var error in validation.Errors)
            {
                Console.Error.WriteLine($"TeamsApiDrift: {error}");
            }
        }
        return validation.Valid ? 0 : 1;
    }

    private static void PrintUsage() => Console.Error.WriteLine(
        "Commands: resolve-version, compare, collect-usage, validate-usage, validate-metadata, classify, write-test-summary, render-report, prepare-agent-context, validate-agent-report");
}
