using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Agents.ApiCompat;
using Xunit;

namespace Microsoft.Agents.ApiCompat.Tests;

public sealed class CompatibilityAnalyzerIntegrationTests(CompatibilityAnalyzerIntegrationTests.PackagePairFixture fixture)
    : IClassFixture<CompatibilityAnalyzerIntegrationTests.PackagePairFixture>
{
    [Theory]
    [InlineData(0, false, AnalysisDecision.Pass)]
    [InlineData(1, false, AnalysisDecision.Block)]
    [InlineData(1, true, AnalysisDecision.Overridden)]
    public void Decide_UsesBlockingFindingsAndOverride(int blockingCount, bool overrideValid, AnalysisDecision expected)
    {
        Assert.Equal(expected, CompatibilityAnalyzer.Decide(blockingCount, overrideValid, Array.Empty<string>()));
    }

    [Fact]
    public void Decide_InfrastructureErrorsAlwaysWin()
    {
        Assert.Equal(
            AnalysisDecision.InfrastructureFailure,
            CompatibilityAnalyzer.Decide(0, overrideValid: true, new[] { "boom" }));
    }

    [Fact]
    public async Task AnalyzeAsync_NoBreakingChange_Passes()
    {
        using var workspace = Workspace.Create();
        workspace.AddProject(fixture.NoBreak.PackageId);
        workspace.AddCandidate(fixture.NoBreak.Candidate);

        var report = await AnalyzeAsync(workspace, WithFeed(fixture.NoBreak));

        Assert.Equal(AnalysisDecision.Pass, report.Decision);
        var package = Assert.Single(report.Packages);
        Assert.Empty(package.Findings);
        Assert.Empty(report.InfrastructureErrors);
    }

    [Fact]
    public async Task AnalyzeAsync_SourceOnlyRename_Blocks()
    {
        using var workspace = Workspace.Create();
        workspace.AddProject(fixture.Rename.PackageId);
        workspace.AddCandidate(fixture.Rename.Candidate);

        var report = await AnalyzeAsync(workspace, WithFeed(fixture.Rename));

        Assert.Equal(AnalysisDecision.Block, report.Decision);
        var finding = Assert.Single(Assert.Single(report.Packages).Findings);
        Assert.Equal("CP0017", finding.DiagnosticId);
        Assert.Equal(CompatibilityCategory.Source, finding.Category);
        Assert.Equal(FindingSeverity.Blocking, finding.Severity);
    }

    [Fact]
    public async Task AnalyzeAsync_BothCategoryRemoval_Blocks()
    {
        using var workspace = Workspace.Create();
        workspace.AddProject(fixture.Removal.PackageId);
        workspace.AddCandidate(fixture.Removal.Candidate);

        var report = await AnalyzeAsync(workspace, WithFeed(fixture.Removal));

        Assert.Equal(AnalysisDecision.Block, report.Decision);
        var finding = Assert.Single(Assert.Single(report.Packages).Findings);
        Assert.Equal("CP0002", finding.DiagnosticId);
        Assert.Equal(CompatibilityCategory.SourceAndBinary, finding.Category);
        Assert.Equal(FindingSeverity.Blocking, finding.Severity);
    }

    [Fact]
    public async Task AnalyzeAsync_WarningOnlyAddition_Passes()
    {
        using var workspace = Workspace.Create();
        workspace.AddProject(fixture.Addition.PackageId);
        workspace.AddCandidate(fixture.Addition.Candidate);

        var report = await AnalyzeAsync(workspace, WithFeed(fixture.Addition));

        Assert.Equal(AnalysisDecision.Pass, report.Decision);
        var finding = Assert.Single(Assert.Single(report.Packages).Findings);
        Assert.Equal(CompatibilityCategory.PotentialSourceRisk, finding.Category);
        Assert.Equal(FindingSeverity.Warning, finding.Severity);
    }

    [Fact]
    public async Task AnalyzeAsync_ValidOverride_Overridden()
    {
        using var workspace = Workspace.Create();
        workspace.AddProject(fixture.Removal.PackageId);
        workspace.AddCandidate(fixture.Removal.Candidate);

        var pullRequest = new PullRequestEvent(
            1,
            45,
            "main",
            "## Breaking change justification\nWe intentionally removed the API.",
            new[] { OverridePolicy.ApprovalLabel });

        var report = await AnalyzeAsync(workspace, WithFeed(fixture.Removal), pullRequest);

        Assert.Equal(AnalysisDecision.Overridden, report.Decision);
        Assert.True(report.Override.IsValid);
    }

    [Fact]
    public async Task AnalyzeAsync_InvalidOverride_Blocks()
    {
        using var workspace = Workspace.Create();
        workspace.AddProject(fixture.Removal.PackageId);
        workspace.AddCandidate(fixture.Removal.Candidate);

        var pullRequest = new PullRequestEvent(
            1,
            45,
            "main",
            "No justification section here.",
            new[] { OverridePolicy.ApprovalLabel });

        var report = await AnalyzeAsync(workspace, WithFeed(fixture.Removal), pullRequest);

        Assert.Equal(AnalysisDecision.Block, report.Decision);
        Assert.False(report.Override.IsValid);
    }

    [Fact]
    public async Task AnalyzeAsync_NoBaseline_Passes()
    {
        using var workspace = Workspace.Create();
        workspace.AddProject(fixture.NoBreak.PackageId);
        workspace.AddCandidate(fixture.NoBreak.Candidate);

        var report = await AnalyzeAsync(workspace, new NuGetStub());

        Assert.Equal(AnalysisDecision.Pass, report.Decision);
        var package = Assert.Single(report.Packages);
        Assert.Equal("NoBaseline", package.Status);
        Assert.Null(package.BaselineVersion);
        Assert.Empty(package.Findings);
    }

    [Fact]
    public async Task AnalyzeAsync_MixedPackages_ReportsPerPackageAndBlocks()
    {
        using var workspace = Workspace.Create();
        workspace.AddProject(fixture.NoBreak.PackageId);
        workspace.AddProject(fixture.Removal.PackageId);
        workspace.AddCandidate(fixture.NoBreak.Candidate);
        workspace.AddCandidate(fixture.Removal.Candidate);

        var stub = new NuGetStub();
        stub.Add(fixture.NoBreak);
        stub.Add(fixture.Removal);

        var report = await AnalyzeAsync(workspace, stub);

        Assert.Equal(AnalysisDecision.Block, report.Decision);
        Assert.Equal(2, report.Packages.Count);
        Assert.Contains(report.Packages, p => p.PackageId == fixture.NoBreak.PackageId && p.Findings.Count == 0);
        Assert.Contains(report.Packages, p => p.PackageId == fixture.Removal.PackageId && p.Findings.Count == 1);
    }

    [Fact]
    public async Task AnalyzeAsync_MissingCandidatePackage_ReportsInfrastructureFailure()
    {
        using var workspace = Workspace.Create();
        workspace.AddProject(fixture.NoBreak.PackageId);

        var report = await AnalyzeAsync(workspace, WithFeed(fixture.NoBreak));

        Assert.Equal(AnalysisDecision.InfrastructureFailure, report.Decision);
        Assert.NotEmpty(report.InfrastructureErrors);
        Assert.Contains(report.InfrastructureErrors, error => error.Contains(fixture.NoBreak.PackageId, StringComparison.Ordinal));
    }

    private static NuGetStub WithFeed(PackagePair pair)
    {
        var stub = new NuGetStub();
        stub.Add(pair);
        return stub;
    }

    private static async Task<CompatibilityReport> AnalyzeAsync(Workspace workspace, NuGetStub stub, PullRequestEvent? pullRequest = null)
    {
        using var client = new HttpClient(stub);
        var analyzer = new CompatibilityAnalyzer(new NuGetBaselineResolver(client));
        var options = new AnalysisOptions(
            workspace.Root,
            workspace.CandidateDirectory,
            workspace.OutputDirectory,
            pullRequest ?? new PullRequestEvent(1, 45, "main", string.Empty, Array.Empty<string>()));

        return await analyzer.AnalyzeAsync(options, CancellationToken.None);
    }

    public sealed record PackagePair(string PackageId, string Version, string Baseline, string Candidate);

    private sealed class NuGetStub : HttpMessageHandler
    {
        private readonly Dictionary<string, (string Version, byte[] Bytes)> _packages =
            new(StringComparer.OrdinalIgnoreCase);

        public void Add(PackagePair pair)
        {
            _packages[pair.PackageId] = (pair.Version, File.ReadAllBytes(pair.Baseline));
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var packageId = segments.Length > 1 ? segments[1] : string.Empty;

            if (path.EndsWith("index.json", StringComparison.Ordinal))
            {
                return Task.FromResult(_packages.TryGetValue(packageId, out var indexEntry)
                    ? Json($$"""{"versions":["{{indexEntry.Version}}"]}""")
                    : new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            if (path.EndsWith(".nupkg", StringComparison.Ordinal) && _packages.TryGetValue(packageId, out var packageEntry))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(packageEntry.Bytes),
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private static HttpResponseMessage Json(string content) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json"),
        };
    }

    private sealed class Workspace : IDisposable
    {
        public string Root { get; }

        public string CandidateDirectory { get; }

        public string OutputDirectory { get; }

        private Workspace(string root)
        {
            Root = root;
            CandidateDirectory = Path.Combine(root, "candidates");
            OutputDirectory = Path.Combine(root, "output");
            Directory.CreateDirectory(Path.Combine(root, "src", "libraries"));
            Directory.CreateDirectory(CandidateDirectory);
            Directory.CreateDirectory(OutputDirectory);
        }

        public static Workspace Create() =>
            new(Path.Combine(AppContext.BaseDirectory, $"analyze-{Guid.NewGuid():N}"));

        public void AddProject(string packageId)
        {
            var projectDirectory = Path.Combine(Root, "src", "libraries", packageId);
            Directory.CreateDirectory(projectDirectory);
            File.WriteAllText(
                Path.Combine(projectDirectory, $"{packageId}.csproj"),
                $"<Project><PropertyGroup><PackageId>{packageId}</PackageId></PropertyGroup></Project>");
        }

        public void AddCandidate(string nupkgPath) =>
            File.Copy(nupkgPath, Path.Combine(CandidateDirectory, Path.GetFileName(nupkgPath)), overwrite: true);

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    public sealed class PackagePairFixture : IAsyncLifetime
    {
        private const string Version = "1.0.0";
        private readonly List<string> _roots = [];

        public PackagePair NoBreak { get; private set; } = null!;

        public PackagePair Rename { get; private set; } = null!;

        public PackagePair Removal { get; private set; } = null!;

        public PackagePair Addition { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            NoBreak = await BuildPairAsync(
                "Contoso.NoBreak",
                "namespace Fixture; public class Api { public void Kept() { } }",
                "namespace Fixture; public class Api { public void Kept() { } }");

            Rename = await BuildPairAsync(
                "Contoso.Rename",
                "namespace Fixture; public class Api { public void Named(int value) { } }",
                "namespace Fixture; public class Api { public void Named(int renamed) { } }");

            Removal = await BuildPairAsync(
                "Contoso.Removal",
                "namespace Fixture; public class Api { public void Removed() { } public void Kept() { } }",
                "namespace Fixture; public class Api { public void Kept() { } }");

            Addition = await BuildPairAsync(
                "Contoso.Addition",
                "namespace Fixture; public class Api { public void Kept() { } }",
                "namespace Fixture; public class Api { public void Kept() { } public void Added(string value) { } }");
        }

        public Task DisposeAsync()
        {
            foreach (var root in _roots.Distinct(StringComparer.Ordinal))
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }

            return Task.CompletedTask;
        }

        private async Task<PackagePair> BuildPairAsync(string packageId, string baselineSource, string candidateSource)
        {
            var baseline = await TestPackageBuilder.BuildAsync(packageId, Version, baselineSource);
            var candidate = await TestPackageBuilder.BuildAsync(packageId, Version, candidateSource);
            _roots.Add(Path.GetDirectoryName(baseline)!);
            _roots.Add(Path.GetDirectoryName(candidate)!);
            return new PackagePair(packageId, Version, baseline, candidate);
        }
    }
}
