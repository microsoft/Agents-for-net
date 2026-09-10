using System.Text.Json;
using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.Agents.TeamsApiDrift;
using Xunit;

namespace Microsoft.Agents.TeamsApiDrift.Tests;

public sealed class DriftPipelineTests
{
    [Fact]
    public void PackageSourcesDefaultToNuGetOrg()
    {
        var source = Assert.Single(PackageApiService.ResolveSources([], null));
        Assert.Equal("https://api.nuget.org/v3/index.json", source.Source);
    }

    [Fact]
    public void PackageSourcesResolveConfiguredAliasAndPreserveOrder()
    {
        var root = TemporaryDirectory();
        var first = Path.Combine(root, "first");
        var second = Path.Combine(root, "second");
        Directory.CreateDirectory(first);
        Directory.CreateDirectory(second);
        var config = Path.Combine(root, "NuGet.config");
        File.WriteAllText(config, $"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <clear />
                <add key="first-fixture" value="{first}" />
              </packageSources>
              <packageSourceCredentials>
                <first-fixture>
                  <add key="Username" value="fixture-user" />
                  <add key="ClearTextPassword" value="fixture-password" />
                </first-fixture>
              </packageSourceCredentials>
            </configuration>
            """);
        try
        {
            var sources = PackageApiService.ResolveSources(["first-fixture", second], config);
            Assert.Equal([first, second], sources.Select(item => item.Source));
            Assert.Equal("fixture-user", sources[0].Credentials?.Username);
            Assert.Equal("fixture-password", sources[0].Credentials?.PasswordText);

            var byUrl = Assert.Single(PackageApiService.ResolveSources([first], config));
            Assert.Equal("fixture-user", byUrl.Credentials?.Username);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void PackageSourcesRejectMissingConfigurationFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.config");
        Assert.Throws<FileNotFoundException>(() => PackageApiService.ResolveSources([], path));
    }

    [Fact]
    public async Task PackageServiceExtractsPackageFromConfiguredLocalSource()
    {
        var root = TemporaryDirectory();
        var source = Path.Combine(root, "feed");
        Directory.CreateDirectory(source);
        CreateFixturePackage(source, "9.9.9", includeNet8: true);
        var config = Path.Combine(root, "NuGet.config");
        File.WriteAllText(config, $"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <clear />
                <add key="fixture" value="{source}" />
              </packageSources>
            </configuration>
            """);
        try
        {
            var model = await new PackageApiService(["fixture"], config).ExtractAsync("9.9.9");
            Assert.Equal("9.9.9", model.Version);
            Assert.All(model.Frameworks, framework => Assert.NotNull(framework.Asset));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task PackageServiceUsesFirstSourceContainingExactVersion()
    {
        var root = TemporaryDirectory();
        var first = Path.Combine(root, "first");
        var second = Path.Combine(root, "second");
        Directory.CreateDirectory(first);
        Directory.CreateDirectory(second);
        CreateFixturePackage(first, "9.9.9", includeNet8: false);
        CreateFixturePackage(second, "9.9.9", includeNet8: true);
        try
        {
            var model = await new PackageApiService([first, second]).ExtractAsync("9.9.9");
            Assert.Null(model.Frameworks.Single(item => item.TargetFramework == "net8.0").Asset);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task PackageServiceContinuesAfterASourceFailsToDownloadThePackage()
    {
        var root = TemporaryDirectory();
        var first = Path.Combine(root, "first");
        var second = Path.Combine(root, "second");
        Directory.CreateDirectory(first);
        Directory.CreateDirectory(second);
        File.WriteAllText(Path.Combine(first, "Microsoft.Teams.Apps.9.9.9.nupkg"), "not a NuGet package");
        CreateFixturePackage(second, "9.9.9", includeNet8: true);
        try
        {
            var model = await new PackageApiService([first, second]).ExtractAsync("9.9.9");
            Assert.Equal("9.9.9", model.Version);
            Assert.All(model.Frameworks, framework => Assert.NotNull(framework.Asset));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task PackageServiceReportsMissingVersionAcrossSources()
    {
        var root = TemporaryDirectory();
        try
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => new PackageApiService([root]).ExtractAsync("9.9.9"));
            Assert.Contains(root, exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ResolveVersionResolvesCentralProperty()
    {
        const string xml = "<Project><PropertyGroup><Microsoft_Teams_Apps_PkgVer>2.1.0</Microsoft_Teams_Apps_PkgVer></PropertyGroup><ItemGroup><PackageVersion Include=\"Microsoft.Teams.Apps\" Version=\"$(Microsoft_Teams_Apps_PkgVer)\" /></ItemGroup></Project>";
        Assert.Equal("2.1.0", VersionResolver.Resolve(new StringReader(xml)));
    }

    [Fact]
    public void FrameworkAssetSelectionPrefersNearestReferenceAsset()
    {
        using var stream = new MemoryStream();
        using (var writer = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            writer.CreateEntry("lib/netstandard2.0/Microsoft.Teams.Apps.dll");
            writer.CreateEntry("ref/net8.0/Microsoft.Teams.Apps.dll");
            writer.CreateEntry("lib/net10.0/Microsoft.Teams.Apps.dll");
        }
        stream.Position = 0;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        Assert.Equal("ref/net8.0/Microsoft.Teams.Apps.dll", PackageApiService.SelectAsset(archive.Entries, "net10.0")?.FullName);
    }

    [Fact]
    public void ExposedTypeNamesUseExactMembership()
    {
        const string teamsChannel = "Microsoft.Teams.Apps.Schema.TeamsChannel";
        const string teamsChannelData = "Microsoft.Teams.Apps.Schema.TeamsChannelData";
        const string teamsChannelDataBase = "Microsoft.Teams.Apps.Schema.TeamsChannelDataBase";
        const string teamsChannelDataInterface = "Microsoft.Teams.Apps.Schema.ITeamsChannelData";
        var publicApi = new[]
        {
            new ApiSymbolModel(
                teamsChannelData,
                "class",
                "public",
                teamsChannelDataBase,
                [teamsChannelDataInterface],
                [],
                false,
                [new ApiMemberModel("method:Use", "Use", "method", "public", $"System.Void Use({teamsChannelData})", false)])
        };

        var exposedTypeNames = AssemblyUsageCollector.BuildExposedTypeNames(publicApi);

        Assert.Contains(teamsChannelData, exposedTypeNames);
        Assert.Contains(teamsChannelDataBase, exposedTypeNames);
        Assert.Contains(teamsChannelDataInterface, exposedTypeNames);
        Assert.DoesNotContain(teamsChannel, exposedTypeNames);
    }

    [Fact]
    public void PublicApiIncludesOperatorsButExcludesOtherSpecialNameMethods()
    {
        using var stream = File.OpenRead(typeof(OperatorFixture).Assembly.Location);
        using var peReader = new PEReader(stream);
        var type = AssemblyMetadataReader.ReadPublicApi(peReader.GetMetadataReader())
            .Single(symbol => symbol.Name == typeof(OperatorFixture).FullName);

        Assert.Contains(type.Members, member => member.Name == "op_Addition");
        Assert.Contains(type.Members, member => member.Name == "op_Implicit");
        Assert.DoesNotContain(type.Members, member => member.Name == "get_Value");
    }

    [Fact]
    public void CompareAggregatesFrameworksAndAssignsDeterministicIds()
    {
        var oldSymbol = Symbol("Contoso.Used", Member("Run"));
        var newSymbol = Symbol("Contoso.Used");
        var before = Model("1.0.0", oldSymbol, oldSymbol);
        var after = Model("2.0.0", newSymbol, newSymbol);

        var first = ApiComparer.Compare(before, after);
        var second = ApiComparer.Compare(before, after);

        var change = Assert.Single(first.Changes);
        Assert.Equal("MTAPI-0001", change.Id);
        Assert.Equal("member-removed", change.Kind);
        Assert.Equal(["net10.0", "net8.0"], change.TargetFrameworks);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
    }

    [Theory]
    [InlineData("class", "System.String Name [nullability:010001]", "System.String Name [nullability:010002]", "nullability-changed")]
    [InlineData("class", "System.String Run()", "System.Int32 Run()", "member-signature-changed")]
    [InlineData("enum", "System.Int32 Value = 1", "System.Int32 Value = 2", "enum-value-changed")]
    public void CompareNamesIncompatibleMemberChangeKinds(string symbolKind, string beforeSignature, string afterSignature, string expectedKind)
    {
        var beforeMember = new ApiMemberModel($"field:Value:{beforeSignature}", "Value", "field", "public", beforeSignature, false);
        var afterMember = new ApiMemberModel($"field:Value:{afterSignature}", "Value", "field", "public", afterSignature, false);
        var before = new ApiSymbolModel("Contoso.Used", symbolKind, "public", null, [], [], false, [beforeMember]);
        var after = new ApiSymbolModel("Contoso.Used", symbolKind, "public", null, [], [], false, [afterMember]);

        Assert.Equal(expectedKind, Assert.Single(ApiComparer.Compare(Model("1.0.0", before, before), Model("2.0.0", after, after)).Changes).Kind);
    }

    [Theory]
    [InlineData("symbol-removed", "breaking", "publicly-exposed", "blocking")]
    [InlineData("base-type-changed", "potentially-breaking", "publicly-exposed", "blocking")]
    [InlineData("base-type-changed", "potentially-breaking", "internal-only", "required")]
    [InlineData("deprecation-added", "unknown", "internal-only", "review")]
    [InlineData("member-added", "non-breaking", "internal-only", "review")]
    public void ClassifyImplementsCompatibilityPolicy(string kind, string compatibility, string exposure, string expected)
    {
        var comparison = Comparison(new ApiChange("MTAPI-0001", kind, "Contoso.Used", null, "before", "after", compatibility, ["net8.0"], ["test"]));
        var manifest = Manifest(new UsageEntry { UpstreamSymbol = "Contoso.Used", Exposure = exposure, Files = ["used.cs"] });
        var capability = Capabilities(("mapped", new Capability { UpstreamTypes = ["Contoso.Used"], AdoptionPolicy = "review-new-members" }));

        var finding = Assert.Single(FindingClassifier.Classify(comparison, manifest, capability).Findings);

        Assert.Equal(expected, finding.Classification);
    }

    [Fact]
    public void ClassifyLeavesUnconsumedChangesAsNoAction()
    {
        var comparison = Comparison(new ApiChange("MTAPI-0001", "symbol-removed", "Contoso.Unused", null, "x", null, "breaking", ["net8.0"], ["test"]));
        Assert.Equal("no-action", Assert.Single(FindingClassifier.Classify(comparison, Manifest(), Capabilities()).Findings).Classification);
    }

    [Fact]
    public void UsageValidationFindsMissingSymbolMemberExposureVersionAndPath()
    {
        var manifest = Manifest(new UsageEntry
        {
            UpstreamSymbol = "Contoso.Used", Members = ["Old"], Exposure = "internal-only", Files = ["../escape.cs"]
        });
        manifest.DeclaredVersion = "1.0.0";
        var collected = new CollectedUsage(1, PackageConstants.PackageId, "test.dll",
        [
            new("Contoso.Used", ["Run"], "publicly-exposed"),
            new("Contoso.Missing", [], "internal-only")
        ]);

        var result = UsageValidator.Validate(manifest, collected, Model("2.0.0", Symbol("Contoso.Used"), Symbol("Contoso.Used")), Path.GetTempPath());

        Assert.False(result.Valid);
        Assert.Contains("Contoso.Missing", result.MissingSymbols);
        Assert.Contains("Contoso.Used.Run", result.MissingMembers);
        Assert.Contains(result.Errors, error => error.Contains("publicly exposed", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("does not match", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("Invalid manifest source path", StringComparison.Ordinal));
    }

    [Fact]
    public void EmptyManifestMemberListIntentionallyCoversAllMembers()
    {
        var manifest = Manifest(new UsageEntry { UpstreamSymbol = "Contoso.Used", Members = [], Exposure = "internal-only" });
        var collected = new CollectedUsage(1, PackageConstants.PackageId, "test.dll", [new("Contoso.Used", ["Run"], "internal-only")]);
        var result = UsageValidator.Validate(manifest, collected, Model("2.1.0", Symbol("Contoso.Used"), Symbol("Contoso.Used")), Path.GetTempPath());
        Assert.True(result.Valid);
    }

    [Fact]
    public void ReportRendererOrdersFindingsByStableId()
    {
        var findings = FindingResult(
            Finding("MTAPI-0002", "blocking"),
            Finding("MTAPI-0001", "blocking"));
        var report = DeterministicReportRenderer.Render(findings);
        Assert.True(report.IndexOf("MTAPI-0001", StringComparison.Ordinal) < report.IndexOf("MTAPI-0002", StringComparison.Ordinal));
    }

    [Fact]
    public void AgentReportValidationAcceptsSummaryPrefixAndAllMandatoryIds()
    {
        var report = ValidReport("This is an advisory report; it does not make or authorize implementation decisions. Additional context.", "MTAPI-0001");
        Assert.True(AgentReportValidator.Validate(report, FindingResult(Finding("MTAPI-0001", "blocking"))).Valid);
    }

    [Fact]
    public void AgentReportValidationAllowsGenericValidationChecklistItems()
    {
        var report = ValidReport("This is an advisory report; it does not make or authorize implementation decisions.", "MTAPI-0001")
            .Replace("## Validation checklist\nText.", "## Validation checklist\n- [ ] Build the extension against the candidate package.", StringComparison.Ordinal);
        Assert.True(AgentReportValidator.Validate(report, FindingResult(Finding("MTAPI-0001", "blocking"))).Valid);
    }

    [Theory]
    [InlineData("MTAPI-9999", "Unknown finding")]
    [InlineData("", "Missing mandatory")]
    public void AgentReportValidationRejectsUnknownOrMissingIds(string ids, string expectedError)
    {
        var result = AgentReportValidator.Validate(ValidReport("This is an advisory report; it does not make or authorize implementation decisions.", ids), FindingResult(Finding("MTAPI-0001", "required")));
        Assert.False(result.Valid);
        Assert.Contains(result.Errors, error => error.Contains(expectedError, StringComparison.Ordinal));
    }

    [Fact]
    public void AgentContextRejectsPathTraversal()
    {
        var finding = Finding("MTAPI-0001", "blocking") with { AffectedFiles = ["../outside.cs"] };
        Assert.Throws<InvalidDataException>(() => AgentContextBuilder.Build(FindingResult(finding), Manifest(), Capabilities(), "report", null, Path.GetTempPath()));
    }

    [Fact]
    public void AgentContextRejectsSymlinkedSourceDirectory()
    {
        var root = TemporaryDirectory();
        var sourceRoot = Path.Combine(root, PackageConstants.SourceRoot);
        var outside = Path.Combine(root, "outside");
        var linkedDirectory = Path.Combine(sourceRoot, "linked");
        var relative = Path.Combine(PackageConstants.SourceRoot, "linked", "outside.cs");
        try
        {
            Directory.CreateDirectory(sourceRoot);
            Directory.CreateDirectory(outside);
            File.WriteAllText(Path.Combine(outside, "outside.cs"), "public class Outside { }");
            try
            {
                Directory.CreateSymbolicLink(linkedDirectory, outside);
            }
            catch (IOException) when (OperatingSystem.IsWindows())
            {
                // Creating links requires a Windows privilege that is not available in every test environment.
                return;
            }

            var finding = Finding("MTAPI-0001", "blocking") with { AffectedFiles = [relative] };
            Assert.Throws<InvalidDataException>(() => AgentContextBuilder.Build(FindingResult(finding), Manifest(), Capabilities(), "report", null, root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AgentContextRedactsAndTruncatesSource()
    {
        var root = Path.Combine(Path.GetTempPath(), $"teams-api-drift-{Guid.NewGuid():N}");
        var relative = $"{PackageConstants.SourceRoot}/test.cs";
        var file = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, "var clientSecret = \"do-not-leak\";" + new string('x', 13_000));
        try
        {
            var finding = Finding("MTAPI-0001", "blocking") with { AffectedFiles = [relative] };
            var json = JsonSerializer.Serialize(AgentContextBuilder.Build(FindingResult(finding), Manifest(), Capabilities(), "report", null, root));
            Assert.DoesNotContain("do-not-leak", json, StringComparison.Ordinal);
            Assert.Contains("[REDACTED]", json, StringComparison.Ordinal);
            Assert.Contains("\"truncated\":true", json, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AgentReportValidationRejectsDuplicateSectionsAndUnattributedActions()
    {
        var report = ValidReport("This is an advisory report; it does not make or authorize implementation decisions.", "MTAPI-0001")
            .ReplaceLineEndings("\n")
            .Replace("## Suggested implementation issues\nText.", "## Suggested implementation issues\n- Update the extension.", StringComparison.Ordinal)
            + "\n## Summary\n- Update the extension.\n";
        var validation = AgentReportValidator.Validate(report, FindingResult(Finding("MTAPI-0001", "blocking")));
        Assert.False(validation.Valid);
        Assert.Contains(validation.Errors, error => error.Contains("exactly once", StringComparison.Ordinal));
        Assert.Contains(validation.Errors, error => error.Contains("not tied", StringComparison.Ordinal));
    }

    private static ApiMemberModel Member(string name) => new($"method:{name}:System.Void {name}()", name, "method", "public", $"System.Void {name}()", false);
    private static string TemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"teams-api-drift-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
    private static void CreateFixturePackage(string source, string version, bool includeNet8)
    {
        var path = Path.Combine(source, $"Microsoft.Teams.Apps.{version}.nupkg");
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        var nuspec = archive.CreateEntry("Microsoft.Teams.Apps.nuspec");
        using (var writer = new StreamWriter(nuspec.Open()))
        {
            writer.Write($"<package><metadata><id>Microsoft.Teams.Apps</id><version>{version}</version><authors>test</authors><description>test</description></metadata></package>");
        }

        var assembly = File.ReadAllBytes(typeof(DriftPipelineTests).Assembly.Location);
        foreach (var framework in includeNet8 ? new[] { "net8.0", "net10.0" } : new[] { "net10.0" })
        {
            var entry = archive.CreateEntry($"lib/{framework}/Microsoft.Teams.Apps.dll");
            using var stream = entry.Open();
            stream.Write(assembly);
        }
    }
    private static ApiSymbolModel Symbol(string name, params ApiMemberModel[] members) => new(name, "class", "public", "System.Object", [], [], false, members);
    private static ApiModel Model(string version, ApiSymbolModel net8, ApiSymbolModel net10) => new(1, PackageConstants.PackageId, version,
    [
        new("net8.0", "lib/net8.0/test.dll", [net8]),
        new("net10.0", "lib/net10.0/test.dll", [net10])
    ]);
    private static ApiComparison Comparison(params ApiChange[] changes) => new(1, PackageConstants.PackageId, "1.0.0", "2.0.0", changes.Length > 0, changes);
    private static UsageManifest Manifest(params UsageEntry[] entries) => new() { DeclaredVersion = "2.1.0", Usages = [.. entries] };
    private static CapabilityDocument Capabilities(params (string Name, Capability Value)[] entries) => new() { Capabilities = entries.ToDictionary(item => item.Name, item => item.Value) };
    private static Finding Finding(string id, string classification) => new(id, classification, null, "member-removed", "Contoso.Used", "Run", null, "internal-only", [], "old", "new", ["net8.0"], ["test"], "Act.");
    private static FindingsResult FindingResult(params Finding[] findings) => new(1, PackageConstants.PackageId, "1.0.0", "2.0.0",
        new(findings.Count(item => item.Classification == "blocking"), findings.Count(item => item.Classification == "required"), findings.Count(item => item.Classification == "review"), findings.Count(item => item.Classification == "no-action")), findings);
    private static string ValidReport(string summary, string ids) => $"""
        # Microsoft.Teams.Apps Impact Report
        ## Summary
        {summary}
        {ids}
        ## Compatibility breaks
        Text.
        ## Required adaptations
        Text.
        ## Feature-review candidates
        Text.
        ## Internal implementation opportunities
        Text.
        ## Maintainer decisions
        Text.
        ## No action
        Text.
        ## Suggested implementation issues
        Text.
        ## Validation checklist
        Text.
        """;
}

public readonly struct OperatorFixture
{
    private readonly int _value;

    public OperatorFixture(int value) => _value = value;

    public int Value => _value;

    public static OperatorFixture operator +(OperatorFixture left, OperatorFixture right) => new(left._value + right._value);

    public static implicit operator int(OperatorFixture value) => value._value;
}
