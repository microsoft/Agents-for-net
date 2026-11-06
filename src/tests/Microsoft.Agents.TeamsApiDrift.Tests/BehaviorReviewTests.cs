using System.IO.Compression;
using System.Text;
using Microsoft.Agents.TeamsApiDrift;
using Xunit;

namespace Microsoft.Agents.TeamsApiDrift.Tests;

public sealed class BehaviorReviewTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "behavior-tests-" + Guid.NewGuid().ToString("N"));
    private static readonly BehaviorCatalog Catalog = new(1, [new("streaming", "Wire behavior", ["StreamingResponse"], [new("text", "Text lifecycle")])]);
    private BehaviorInput Prepare() => BehaviorReviewPipeline.Prepare(_root, Catalog,
        new[] { "agents", "teams-candidate" }.Select(role =>
            (new BehaviorSource(role, "2.1.0", "fixture", new string('a', 40)),
            new Dictionary<string, string> { ["moved/src/Stream.cs"] = "first line\nsecond line\n" })).ToArray());
    private static BehaviorReview Review(BehaviorInput input, string outcome = "equivalent") =>
        new(1, input.ReviewId, [new("streaming", "text", outcome, "Compared serialized requests.",
            outcome == "different" ? "Different final entity." : null, outcome == "different" ? "Potential service compatibility." : null,
            outcome == "different" ? "Verify final payload." : null,
            input.Files.Select(f => new ReviewCitation(f.Path, StartLine: 1, EndLine: 2)).ToArray())]);

    [Fact]
    public void ReportUsesConciseSummaryAndPinnedSourceLinksWithoutCodeBlocks()
    {
        var commit = new string('a', 40);
        var input = BehaviorReviewPipeline.Prepare(_root, Catalog,
            new[] { "agents", "teams-candidate" }.Select(role =>
                (new BehaviorSource(role, "2.1.2", role == "agents" ? "https://github.com/southworks/Agents-for-net" : "https://github.com/microsoft/teams.net", commit),
                new Dictionary<string, string> { ["src/Stream.cs"] = "first line\nsecond line\n" })).ToArray());
        var review = Review(input, "different");
        review = review with { Decisions = [review.Decisions[0] with { Title = "Streaming responses: request format differs" }] };
        var validation = BehaviorReviewPipeline.Validate(input, review, _root);
        Assert.True(validation.Valid);
        var report = BehaviorReviewPipeline.Render(input, validation);
        Assert.Contains("### Streaming responses: request format differs", report);
        Assert.Contains("Different final entity.", report);
        Assert.Contains("**Potential impact:** Potential service compatibility.", report);
        Assert.Contains("**Next step:** Verify final payload.", report);
        Assert.Contains($"https://github.com/southworks/Agents-for-net/blob/{commit}/src/Stream.cs#L1-L2", report);
        Assert.Contains($"https://github.com/microsoft/teams.net/blob/{commit}/src/Stream.cs#L1-L2", report);
        Assert.DoesNotContain("Compared serialized requests.", report);
        Assert.DoesNotContain("```", report);
        Assert.DoesNotContain("first line", report);
        Assert.Equal("first line\nsecond line", validation.Decisions[0].Evidence[0].Quote);
    }

    [Fact]
    public void FixtureSourceReferencesDoNotInventUpstreamLinks()
    {
        var input = Prepare();
        var validation = BehaviorReviewPipeline.Validate(input, Review(input, "different"), _root);
        var report = BehaviorReviewPipeline.Render(input, validation);
        Assert.Contains("Agents SDK snapshot: `moved/src/Stream.cs:1-2`", report);
        Assert.Contains("Teams SDK snapshot: `moved/src/Stream.cs:1-2`", report);
        Assert.DoesNotContain("https://github.com", report);
        Assert.DoesNotContain("```", report);
    }

    [Theory]
    [InlineData("equivalent", false)]
    [InlineData("different", true)]
    [InlineData("inconclusive", true)]
    public void ValidFindingsAreAdvisory(string outcome, bool findings)
    {
        var input = Prepare();
        var result = BehaviorReviewPipeline.Validate(input, Review(input, outcome), _root);
        Assert.True(result.Valid, string.Join("\n", result.Errors));
        Assert.Equal(findings, result.HasFindings);
        Assert.Equal(new[] { "agents", "teams-candidate" }, input.Sources.Select(s => s.Role));
        Assert.Contains("Teams specific behavior analysis", BehaviorReviewPipeline.Render(input, result));
        Assert.All(result.Decisions[0].Evidence, c => Assert.Equal("first line\nsecond line", c.Quote));
        Assert.Contains("Source-based analysis", BehaviorReviewPipeline.Render(input, result));
    }

    [Theory]
    [InlineData("equivalent")]
    [InlineData("different")]
    [InlineData("inconclusive")]
    public void ReportShowsOnlyFindingsWhileValidationRetainsAllDecisions(string outcome)
    {
        var input = Prepare();
        var result = BehaviorReviewPipeline.Validate(input, Review(input, outcome), _root);
        var report = BehaviorReviewPipeline.Render(input, result);

        Assert.True(result.Valid);
        Assert.Equal(outcome, Assert.Single(result.Decisions).Outcome);
        if (outcome == "equivalent")
        {
            Assert.DoesNotContain("### ", report);
            Assert.DoesNotContain("Compared serialized requests.", report);
            Assert.DoesNotContain("**Sources:**", report);
            Assert.Contains("No behavior differences or inconclusive results found.", report);
        }
        else
        {
            Assert.Contains($"### streaming / text: {outcome}", report);
            Assert.Contains("**Sources:**", report);
            Assert.DoesNotContain("No behavior differences or inconclusive results found.", report);
        }
    }

    [Fact]
    public void CoverageRequiresExactlyOneKnownCheck()
    {
        var input = Prepare();
        var review = Review(input);
        Assert.False(BehaviorReviewPipeline.Validate(input, review with { Decisions = [] }, _root).Valid);
        Assert.False(BehaviorReviewPipeline.Validate(input, review with { Decisions = [review.Decisions[0], review.Decisions[0]] }, _root).Valid);
        Assert.False(BehaviorReviewPipeline.Validate(input, review with { Decisions = [review.Decisions[0] with { CheckId = "unknown" }] }, _root).Valid);
    }

    [Fact]
    public void RejectsIdentityAndSourceTampering()
    {
        var input = Prepare();
        Assert.False(BehaviorReviewPipeline.Validate(input, Review(input) with { ReviewId = "stale" }, _root).Valid);
        File.AppendAllText(Path.Combine(_root, input.Files[0].Path), "modified");
        Assert.False(BehaviorReviewPipeline.Validate(input, Review(input), _root).Valid);
    }

    [Theory]
    [InlineData("agents/missing.cs", 1, 2)]
    [InlineData("agents/moved/src/Stream.cs", 0, 2)]
    [InlineData("agents/moved/src/Stream.cs", 1, 999)]
    public void RejectsInvalidCitations(string path, int start, int end)
    {
        var input = Prepare();
        var review = Review(input);
        var decision = review.Decisions[0] with { Evidence = [new(path, StartLine: start, EndLine: end)] };
        Assert.False(BehaviorReviewPipeline.Validate(input, review with { Decisions = [decision] }, _root).Valid);
    }

    [Theory]
    [InlineData("agents/")]
    [InlineData("teams-candidate/")]
    public void ConclusionsRequireBothSdks(string omittedRole)
    {
        var input = Prepare();
        var review = Review(input);
        var decision = review.Decisions[0];
        var evidence = decision.Evidence.Where(c => !c.Path.StartsWith(omittedRole)).ToArray();
        Assert.False(BehaviorReviewPipeline.Validate(input, review with { Decisions = [decision with { Evidence = evidence }] }, _root).Valid);
    }

    [Fact]
    public void InconclusiveCanDescribeMissingEvidenceWithoutFabricatedCitations()
    {
        var input = Prepare();
        var review = Review(input, "inconclusive");
        Assert.True(BehaviorReviewPipeline.Validate(input, review with { Decisions = [review.Decisions[0] with { Evidence = [] }] }, _root).Valid);
    }

    [Fact]
    public void DifferenceRequiresActionableWireDetails()
    {
        var input = Prepare();
        var review = Review(input, "different");
        Assert.False(BehaviorReviewPipeline.Validate(input, review with { Decisions = [review.Decisions[0] with { WireEffect = null }] }, _root).Valid);
    }

    [Theory]
    [InlineData("2.1.0", "https://github.com/microsoft/teams.net", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)]
    [InlineData("2.1.0", "https://github.com/microsoft/teams.net.git", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)]
    [InlineData("2.1.1", "https://github.com/microsoft/teams.net", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", false)]
    [InlineData("2.1.0", "https://example.com/untrusted", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", false)]
    [InlineData("2.1.0", "https://github.com/microsoft/teams.net", "main", false)]
    [InlineData("2.1.0", "https://github.com/microsoft/teams.net", "", false)]
    public void ResolvesOnlyExactPackageProvenance(string version, string url, string commit, bool valid)
    {
        using var xml = new MemoryStream(Encoding.UTF8.GetBytes($"<package><metadata><id>Microsoft.Teams.Apps</id><version>{version}</version><repository url=\"{url}\" commit=\"{commit}\" /></metadata></package>"));
        if (valid) Assert.Equal(commit, BehaviorReviewPipeline.ReadProvenance(xml, "2.1.0", "teams-candidate").Commit);
        else Assert.Throws<InvalidDataException>(() => BehaviorReviewPipeline.ReadProvenance(xml, "2.1.0", "teams-candidate"));
    }

    [Theory]
    [InlineData("repo/core/src/Stream.cs", "core/src/Stream.cs")]
    [InlineData("repo/src/Stream.cs", "src/Stream.cs")]
    public void ArchivePreservesRevisionSpecificPaths(string archived, string expected)
    {
        using var bytes = new MemoryStream();
        using (var zip = new ZipArchive(bytes, ZipArchiveMode.Create, true))
        {
            using var writer = new StreamWriter(zip.CreateEntry(archived).Open());
            writer.Write("class Stream {}");
        }
        bytes.Position = 0;
        using var read = new ZipArchive(bytes, ZipArchiveMode.Read);
        Assert.True(BehaviorReviewPipeline.ReadArchive(read).ContainsKey(expected));
    }

    [Fact]
    public void MissingSourcesAndReusedWorkspacesFailPreparation()
    {
        var input = Prepare();
        Assert.Throws<InvalidDataException>(() => Prepare());
        Assert.Throws<InvalidDataException>(() => BehaviorReviewPipeline.Prepare(_root + "-empty", Catalog, []));
        Assert.NotEmpty(input.Files);
    }

    [Fact]
    public void UnavailableSourceFailsPreparation()
    {
        var sources = new[] { "agents", "teams-candidate" }.Select(role =>
            (new BehaviorSource(role, "2.1.0", "fixture", new string('a', 40)), new Dictionary<string, string>())).ToArray();
        Assert.Throws<InvalidDataException>(() => BehaviorReviewPipeline.Prepare(_root, Catalog, sources));
    }

    [Fact]
    public void SnapshotInputCannotBeChanged()
    {
        var input = Prepare();
        File.WriteAllText(Path.Combine(_root, "behavior-input.json"), "{}");
        Assert.False(BehaviorReviewPipeline.Validate(input, Review(input), _root).Valid);
    }

    [Theory]
    [InlineData("repo/../escape.cs")]
    [InlineData("repo/src/../../escape.cs")]
    public void ArchiveRejectsTraversal(string path)
    {
        using var bytes = new MemoryStream();
        using (var zip = new ZipArchive(bytes, ZipArchiveMode.Create, true))
        {
            using var writer = new StreamWriter(zip.CreateEntry(path).Open());
            writer.Write("class Stream {}");
        }
        bytes.Position = 0;
        using var read = new ZipArchive(bytes, ZipArchiveMode.Read);
        Assert.Throws<InvalidDataException>(() => BehaviorReviewPipeline.ReadArchive(read));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
