using System.Text.Json;
using Microsoft.Agents.TeamsApiDrift;
using Xunit;

namespace Microsoft.Agents.TeamsApiDrift.Tests;

public sealed class ImpactReviewTests
{
    private const string Source = PackageConstants.SourceRoot + "/Mapper.cs";
    private const string Test = "src/tests/Microsoft.Agents.Extensions.MSTeams.Tests/MapperTests.cs";
    private const string Implementation = "    internal static string? Map(Settings value) => value.Name?.Trim();";
    private const string Assertion = "    public void NullIsHandled() { Assert.Null(Map(new Settings { Name = null })); }";

    [Fact]
    public void SafeReviewResolvesQuoteLocationsAndPassesGate()
    {
        using var fixture = new Fixture();
        var result = fixture.Validate(fixture.Review());
        Assert.True(result.Valid, string.Join(" ", result.Errors));
        Assert.True(result.CanProceed);
        Assert.Equal(1, result.Summary.Safe);
        Assert.All(result.Decisions[0].Evidence, citation => Assert.Equal(2, citation.StartLine));
        Assert.Contains("Mapper.cs:2-2", ImpactReviewPipeline.Render(fixture.Input, result));
    }

    [Fact]
    public void RangeOnlyEvidenceIsMaterializedFromVerifiedSource()
    {
        using var fixture = new Fixture();
        var review = fixture.Review();
        var decision = review.Decisions[0] with { Evidence = [new(Source, StartLine: 2, EndLine: 2), new(Test, StartLine: 1, EndLine: 2)] };
        // Exercise optional quote deserialization as supplied by the agent.
        var parsed = ImpactReviewPipeline.Parse(JsonSerializer.Serialize(review with { Decisions = [decision] }, ToolJson.Options));
        var result = fixture.Validate(parsed);
        Assert.True(result.CanProceed, string.Join(" ", result.Errors));
        Assert.Equal(Implementation, result.Decisions[0].Evidence[0].Quote);
        Assert.EndsWith(Assertion, result.Decisions[0].Evidence[1].Quote, StringComparison.Ordinal);
        Assert.Contains("MapperTests.cs:1-2", ImpactReviewPipeline.Render(fixture.Input, result));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(2, null)]
    [InlineData(null, 2)]
    [InlineData(0, 2)]
    [InlineData(3, 2)]
    [InlineData(1, 999)]
    [InlineData(5, 5)]
    public void InvalidOrEmptyRangesCannotSupplyEvidence(int? start, int? end)
    {
        using var fixture = new Fixture();
        var review = fixture.Review();
        var decision = review.Decisions[0] with { Evidence = [new(Source, StartLine: start, EndLine: end), new(Test, Assertion)] };
        Assert.False(fixture.Validate(review with { Decisions = [decision] }).CanProceed);
    }

    [Theory]
    [InlineData("needs-adaptation", "used")]
    [InlineData("needs-human-review", "uncertain")]
    public void CompleteNonSafeReviewsValidateButBlockGate(string verdict, string usage)
    {
        using var fixture = new Fixture();
        var review = fixture.Review(verdict, usage);
        var result = fixture.Validate(review);
        Assert.True(result.Valid, string.Join(" ", result.Errors));
        Assert.False(result.CanProceed);
        Assert.Equal(0, result.Summary.Safe);
    }

    [Theory]
    [InlineData("recommend-adoption", 1, 0, 0)]
    [InlineData("no-adoption-needed", 0, 1, 0)]
    [InlineData("needs-product-decision", 0, 0, 1)]
    public void CompletedAdoptionOutcomesAreReportedAndNeverBlockSafeCompatibility(string outcome, int recommend, int notNeeded, int productDecision)
    {
        using var fixture = new Fixture(unused: true, additionKind: "member-added");
        var review = fixture.Review("safe", "unused");
        var decision = review.Decisions[0] with { Evidence = [new(Source, Implementation)], Adoption = new(outcome, "Feature-specific adoption reasoning.") };
        var parsed = ImpactReviewPipeline.Parse(JsonSerializer.Serialize(review with { Decisions = [decision] }, ToolJson.Options));
        var result = fixture.Validate(parsed);
        Assert.True(result.CanProceed, string.Join(" ", result.Errors));
        Assert.Equal(recommend, result.Summary.AdoptionRecommendations);
        Assert.Equal(notNeeded, result.Summary.AdoptionNotNeeded);
        Assert.Equal(productDecision, result.Summary.AdoptionNeedsProductDecision);
        var report = ImpactReviewPipeline.Render(fixture.Input, result);
        Assert.Contains("Feature adoption assessments (nonblocking)", report);
        Assert.Contains("Additions assessed: **1/1**", report);
        Assert.Contains(outcome, report);
        Assert.Contains(decision.Adoption.Reason, report);
    }

    [Theory]
    [InlineData("symbol-added", false)]
    [InlineData("symbol-added", true)]
    [InlineData("member-added", false)]
    [InlineData("member-added", true)]
    [InlineData("framework-asset-added", false)]
    [InlineData("framework-asset-added", true)]
    public void EveryAdditionRejectsOmittedOrNullAdoption(string kind, bool explicitNull)
    {
        using var fixture = new Fixture(additionKind: kind);
        Assert.Equal(kind, Assert.Single(fixture.Input.Comparison.Changes).Kind);
        var json = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(fixture.Review(), ToolJson.Options))!;
        var decision = json["decisions"]![0]!.AsObject();
        if (explicitNull) decision["adoption"] = null;
        else decision.Remove("adoption");
        var result = fixture.Validate(ImpactReviewPipeline.Parse(json.ToJsonString()));
        Assert.False(result.Valid);
        Assert.False(result.CanProceed);
        Assert.Contains(result.Errors, error => error.Contains("Missing adoption assessment for addition", StringComparison.Ordinal));
        Assert.Contains("Additions assessed: **0/1**", ImpactReviewPipeline.Render(fixture.Input, result));
    }

    [Theory]
    [InlineData(null, "Reason")]
    [InlineData("", "Reason")]
    [InlineData("safe", "Reason")]
    [InlineData("recommend-adoption", null)]
    [InlineData("no-adoption-needed", " ")]
    [InlineData("needs-product-decision", "")]
    public void InvalidAdoptionCannotCountAsCompletedAssessment(string? outcome, string? reason)
    {
        using var fixture = new Fixture(additionKind: "member-added");
        var review = fixture.Review();
        var decision = review.Decisions[0] with { Adoption = new(outcome!, reason!) };
        var result = fixture.Validate(review with { Decisions = [decision] });
        Assert.False(result.Valid);
        Assert.False(result.CanProceed);
        Assert.Contains("Additions assessed: **0/1**", ImpactReviewPipeline.Render(fixture.Input, result));
        Assert.Equal(0, result.Summary.AdoptionRecommendations + result.Summary.AdoptionNotNeeded + result.Summary.AdoptionNeedsProductDecision);
    }

    [Fact]
    public void EveryAdditionNeedsAssessmentEvenWhenRelatedChangesShareARecommendation()
    {
        using var fixture = new Fixture(additionKind: "mixed");
        var review = fixture.Review();
        Assert.Equal(3, review.Decisions.Count);
        var adoption = new AdoptionAssessment("recommend-adoption", "Expose the related capability through one feature change.");
        review = review with { Decisions = review.Decisions.Select(decision => decision with { Adoption = adoption }).ToArray() };
        var result = fixture.Validate(review);
        Assert.True(result.CanProceed, string.Join(" ", result.Errors));
        Assert.Equal(3, result.Summary.AdoptionRecommendations);
        var report = ImpactReviewPipeline.Render(fixture.Input, result);
        Assert.Contains("Additions assessed: **3/3**", report);
        Assert.Contains(string.Join(", ", review.Decisions.Select(decision => decision.ChangeId)), report);
        Assert.Equal(1, report.Split(adoption.Reason, StringSplitOptions.None).Length - 1);
        var incomplete = fixture.Validate(review with { Decisions = review.Decisions.Select((decision, index) => index == 1 ? decision with { Adoption = null } : decision).ToArray() });
        Assert.False(incomplete.Valid);
        Assert.Contains("Additions assessed: **2/3**", ImpactReviewPipeline.Render(fixture.Input, incomplete));
    }

    [Fact]
    public void NonAdditiveChangesDoNotRequireAnAdoptionAssessment()
    {
        using var fixture = new Fixture();
        Assert.Equal("nullability-changed", Assert.Single(fixture.Input.Comparison.Changes).Kind);
        var result = fixture.Validate(fixture.Review());
        Assert.True(result.CanProceed);
        Assert.Equal(0, result.Summary.AdoptionRecommendations);
        Assert.Equal(0, result.Summary.AdoptionNotNeeded);
        Assert.Equal(0, result.Summary.AdoptionNeedsProductDecision);
        Assert.Contains("Additions assessed: **0/0**", ImpactReviewPipeline.Render(fixture.Input, result));
    }

    [Theory]
    [InlineData("needs-human-review", "uncertain", false)]
    [InlineData("needs-adaptation", "used", false)]
    [InlineData("safe", "used", true)]
    public void AdoptionCannotClearCompatibilityOrIndependentFailures(string verdict, string usage, bool failedCheck)
    {
        using var fixture = new Fixture(failedCheck: failedCheck ? "build" : null, additionKind: "member-added");
        var review = fixture.Review(verdict, usage);
        review = review with { Decisions = [review.Decisions[0] with { Adoption = new("recommend-adoption", "Optional future feature work.") }] };
        var result = fixture.Validate(review);
        Assert.True(result.Valid, string.Join(" ", result.Errors));
        Assert.Equal(1, result.Summary.AdoptionRecommendations);
        Assert.False(result.CanProceed);
    }

    [Fact]
    public void AdoptionCannotReplaceRequiredDecisionsOrEvidence()
    {
        using var fixture = new Fixture();
        var review = fixture.Review();
        var decision = review.Decisions[0] with { Adoption = new("recommend-adoption", "Optional future feature work."), Evidence = [] };
        Assert.False(fixture.Validate(review with { Decisions = [decision] }).CanProceed);
        Assert.False(fixture.Validate(review with { Decisions = [] }).CanProceed);
        Assert.False(fixture.Validate(review with { Decisions = [review.Decisions[0] with { Adoption = new("no-adoption-needed", " ") }] }).Valid);
    }

    [Fact]
    public void CoverageRequiresExactlyOneKnownDecisionPerChange()
    {
        using var fixture = new Fixture();
        var review = fixture.Review();
        var decision = review.Decisions[0];
        foreach (var invalid in new[]
        {
            review with { Decisions = [] },
            review with { Decisions = [decision, decision] },
            review with { Decisions = [decision with { ChangeId = "MTAPI-9999" }] },
            review with { Decisions = null! },
            review with { Decisions = [null!] }
        }) Assert.False(fixture.Validate(invalid).Valid);
    }

    [Fact]
    public void InvalidVerdictsUsageAndEmptyReasonCannotPass()
    {
        using var fixture = new Fixture();
        var review = fixture.Review();
        foreach (var decision in new[]
        {
            review.Decisions[0] with { Verdict = "probably-safe" },
            review.Decisions[0] with { Usage = "maybe-used" },
            review.Decisions[0] with { Reason = null! },
            review.Decisions[0] with { Evidence = null! },
            review.Decisions[0] with { Evidence = [null!] }
        })
        {
            var result = fixture.Validate(review with { Decisions = [decision] });
            Assert.False(result.Valid);
            Assert.False(result.CanProceed);
            Assert.Contains("FAILED", ImpactReviewPipeline.Render(fixture.Input, result));
        }
    }

    [Fact]
    public void PublicAndConsumedChangesRequireImplementationAndTestEvidence()
    {
        using var fixture = new Fixture(publicExposure: true);
        var review = fixture.Review();
        foreach (var evidence in new[]
        {
            Array.Empty<ReviewCitation>(),
            new[] { new ReviewCitation(Source, Implementation) },
            new[] { new ReviewCitation(Test, Assertion) }
        }) Assert.False(fixture.Validate(review with { Decisions = [review.Decisions[0] with { Evidence = evidence }] }).Valid);
    }

    [Fact]
    public void RecordedUsageCannotBeDismissedAsUnused()
    {
        using var fixture = new Fixture();
        var result = fixture.Validate(fixture.Review("safe", "unused"));
        Assert.False(result.Valid);
        Assert.Contains(result.Errors, error => error.Contains("contradicts unused", StringComparison.Ordinal));
    }

    [Fact]
    public void TrulyUnrecordedChangesCanBeReviewedAsUnusedWithImplementationEvidence()
    {
        using var fixture = new Fixture(unused: true);
        var review = fixture.Review("safe", "unused");
        review = review with { Decisions = [review.Decisions[0] with { Evidence = [new(Source, Implementation)] }] };
        Assert.True(fixture.Validate(review).CanProceed);
    }

    [Theory]
    [InlineData("tool")]
    [InlineData("compare")]
    [InlineData("build")]
    [InlineData("drift-tests")]
    [InlineData("collect")]
    [InlineData("manifest")]
    [InlineData("contract-net8")]
    [InlineData("contract-net10")]
    [InlineData("boundary-net8")]
    [InlineData("boundary-net10")]
    public void IndependentFailuresCannotBeOverriddenBySafeReview(string failedCheck)
    {
        using var fixture = new Fixture(failedCheck: failedCheck);
        var result = fixture.Validate(fixture.Review());
        Assert.True(result.Valid);
        Assert.False(result.CanProceed);
    }

    [Fact]
    public void ScheduledChecksRequireBothFrameworksRecordedByCombinedCheck()
    {
        var passed = new TestSummary(1, new[] { "tool", "compare", "build", "drift-tests", "collect", "manifest", "candidate-tests" }.ToDictionary(name => name, _ => "success"));
        Assert.True(ImpactReviewPipeline.ChecksPassed(passed));
        Assert.False(ImpactReviewPipeline.ChecksPassed(passed with { Checks = new Dictionary<string, string>() }));
    }

    [Fact]
    public void IdentityBindsVersionsComparisonAndChecks()
    {
        using var fixture = new Fixture();
        var review = fixture.Review();
        foreach (var stale in new[]
        {
            review with { ReviewId = "stale" }, review with { FromVersion = "0.0.0" },
            review with { ToVersion = "9.0.0" }, review with { SchemaVersion = 2 }
        }) Assert.False(fixture.Validate(stale).Valid);
        var altered = fixture.Input with { Checks = new(1, new Dictionary<string, string>()) };
        Assert.False(ImpactReviewPipeline.Validate(review, altered, fixture.Workspace, fixture.Repository).Valid);
    }

    [Theory]
    [InlineData("snapshot")]
    [InlineData("repository")]
    [InlineData("inventory")]
    [InlineData("package-model")]
    public void SourceAndPackageMetadataChangesInvalidateReview(string target)
    {
        using var fixture = new Fixture();
        if (target == "inventory") File.WriteAllText(Path.Combine(fixture.Repository, "new.cs"), "class Changed { }");
        else if (target == "package-model") File.AppendAllText(Path.Combine(fixture.Workspace, "candidate-api.json"), " ");
        else File.AppendAllText(Path.Combine(target == "snapshot" ? fixture.Workspace : fixture.Repository, Source), "\n// changed\n");
        Assert.False(fixture.Validate(fixture.Review()).Valid);
    }

    [Theory]
    [InlineData("fabricated")]
    [InlineData("range")]
    [InlineData("outside")]
    [InlineData("ambiguous")]
    public void CitationsRequireExactUnambiguousSource(string problem)
    {
        using var fixture = new Fixture();
        var review = fixture.Review();
        var citation = problem switch
        {
            "fabricated" => new ReviewCitation(Source, "return something else;"),
            "range" => new ReviewCitation(Source, Implementation, 1, 1),
            "outside" => new ReviewCitation("../outside.cs", Implementation),
            _ => new ReviewCitation(Source, "// repeated")
        };
        Assert.False(fixture.Validate(review with { Decisions = [review.Decisions[0] with { Evidence = [citation, new(Test, Assertion)] }] }).Valid);
    }

    [Fact]
    public void ParserAcceptsPlainJsonOrOneTerminalJsonFenceAfterProgress()
    {
        using var fixture = new Fixture();
        var json = JsonSerializer.Serialize(fixture.Review(), ToolJson.Options);
        Assert.Equal(fixture.Input.ReviewId, ImpactReviewPipeline.Parse(json).ReviewId);
        Assert.Equal(fixture.Input.ReviewId, ImpactReviewPipeline.Parse("```json\r\n" + json + "\r\n```\n").ReviewId);
        Assert.Equal(fixture.Input.ReviewId, ImpactReviewPipeline.Parse("I'll review the candidate.\n\nLet me read the source.\n\n```json\n" + json + "\n```\n").ReviewId);
        Assert.Equal(fixture.Input.ReviewId, ImpactReviewPipeline.Parse("Progress\n```\n" + json + "\n```").ReviewId);
        foreach (var invalid in new[] { "Introduction\n" + json, "```json\n" + json, json + "\nExtra commentary", "{}", json.Replace("\"schemaVersion\": 1", "\"extra\": true, \"schemaVersion\": 1", StringComparison.Ordinal) })
            Assert.ThrowsAny<Exception>(() => ImpactReviewPipeline.Parse(invalid));
    }

    [Fact]
    public void ParserRejectsAmbiguousAndIncompleteFencedResponses()
    {
        using var fixture = new Fixture();
        var json = JsonSerializer.Serialize(fixture.Review(), ToolJson.Options);
        var fenced = "```json\n" + json + "\n```";
        foreach (var invalid in new[]
        {
            fenced + "\n" + fenced, "Progress\n" + fenced + "\nTrailing commentary",
            "Progress\n```json\n" + json, json + "\n" + fenced,
            "```text\nProgress\n```\n" + fenced, "Progress\n```javascript\n" + json + "\n```",
            "Progress\n```json\n{}\n```"
        }) Assert.ThrowsAny<Exception>(() => ImpactReviewPipeline.Parse(invalid));
    }

    [Fact]
    public void ProgressWrapperDoesNotBypassEvidenceOrIdentityValidation()
    {
        using var fixture = new Fixture();
        var review = fixture.Review();
        var decision = review.Decisions[0] with { Evidence = [new("review-input.json", "fabricated metadata")] };
        foreach (var invalid in new[]
        {
            review with { ReviewId = "stale" }, review with { Decisions = [decision] },
            review with { Decisions = [review.Decisions[0] with { Evidence = [new(Source, Implementation.Trim()), new(Test, Assertion)] }] }
        })
        {
            var response = "I'll review the candidate.\n\n```json\n" + JsonSerializer.Serialize(invalid, ToolJson.Options) + "\n```\n";
            var result = fixture.Validate(ImpactReviewPipeline.Parse(response));
            Assert.False(result.Valid);
            Assert.False(result.CanProceed);
        }
    }

    [Fact]
    public void EmptyComparisonValidatesWithoutAgentDecisions()
    {
        using var fixture = new Fixture(noChanges: true);
        Assert.Empty(fixture.Input.Comparison.Changes);
        Assert.True(fixture.Validate(fixture.Review()).CanProceed);
    }

    [Fact]
    public void SnapshotIncludesFullCallersAndRedactsCredentialLiterals()
    {
        using var fixture = new Fixture();
        var file = Path.Combine(fixture.Workspace, "src/Caller.cs");
        Assert.True(File.ReadAllText(file).Length > 13_000);
        Assert.DoesNotContain("do-not-leak", File.ReadAllText(file), StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", File.ReadAllText(file), StringComparison.Ordinal);
        Assert.DoesNotContain("json-do-not-leak", File.ReadAllText(Path.Combine(fixture.Workspace, "src/config.json")), StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() => fixture.Prepare());
    }

    [Fact]
    public void PdbNavigationMapsOriginalAndForeignRunnerPathsIntoSnapshot()
    {
        using var fixture = new Fixture(sourceHints: true);
        Assert.Equal(new[] { Source }, fixture.Input.CollectedUsage!.SourceFiles);
        Assert.Equal(2, fixture.Input.UnmappedSourceFileCount);
        Assert.Equal(6, fixture.OriginalCollected!.SourceFiles!.Count);
        Assert.Contains(fixture.OriginalCollected.SourceFiles, path => path.Contains("/home/runner/", StringComparison.Ordinal));
        foreach (var path in fixture.Input.CollectedUsage.SourceFiles!)
        {
            Assert.Contains(fixture.Input.Files, file => file.Path == path);
            Assert.True(File.Exists(ImpactReviewPipeline.Resolve(fixture.Workspace, path)));
        }
        Assert.True(fixture.Validate(fixture.Review()).CanProceed);
        var altered = fixture.Input with { UnmappedSourceFileCount = 0 };
        Assert.False(ImpactReviewPipeline.Validate(fixture.Review(), altered, fixture.Workspace, fixture.Repository).Valid);
    }

    [Fact]
    public void AmbiguousOrUnknownPdbPathsAreNotUsableNavigationHints()
    {
        var collected = new CollectedUsage(1, PackageConstants.PackageId, "fixture.dll", [], ["/build/" + Source, "/outside/Secret.cs"]);
        var navigation = ImpactReviewPipeline.SnapshotUsage(collected, [new(Source, "hash", "hash"), new("Mapper.cs", "hash", "hash")]);
        Assert.Empty(navigation.Usage!.SourceFiles!);
        Assert.Equal(2, navigation.Unmapped);
        Assert.Equal(2, collected.SourceFiles!.Count);
    }

    [Fact]
    public void PathsCannotEscapeSnapshotOrTraverseLinks()
    {
        using var fixture = new Fixture();
        Assert.Throws<InvalidDataException>(() => ImpactReviewPipeline.Resolve(fixture.Workspace, "../outside.cs"));
        var link = Path.Combine(fixture.Workspace, "link");
        try { Directory.CreateSymbolicLink(link, fixture.Repository); }
        catch (IOException) when (OperatingSystem.IsWindows()) { return; }
        catch (UnauthorizedAccessException) when (OperatingSystem.IsWindows()) { return; }
        Assert.Throws<InvalidDataException>(() => ImpactReviewPipeline.Resolve(fixture.Workspace, "link/src/Caller.cs"));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "impact-review-test-" + Guid.NewGuid().ToString("N"));
        private readonly ApiModel _before;
        private readonly ApiModel _after;
        private readonly UsageManifest _manifest;
        private readonly TestSummary _checks;
        public CollectedUsage? OriginalCollected { get; }
        public string Repository => Path.Combine(_root, "repo");
        public string Workspace => Path.Combine(_root, "snapshot");
        public ImpactReviewInput Input { get; }

        public Fixture(bool publicExposure = false, bool unused = false, bool noChanges = false, string? failedCheck = null, bool sourceHints = false, string? additionKind = null)
        {
            Directory.CreateDirectory(Repository);
            Write(Source, "// fixture\n" + Implementation + "\n// repeated\n// repeated\n");
            Write("src/config.json", "{\"ClientSecret\":\"json-do-not-leak\"}");
            Write(Test, "[Trait(\"Category\", \"TeamsApiBoundary\")]\n" + Assertion + "\n");
            Write("src/Caller.cs", "var clientSecret = \"do-not-leak\";\n// " + new string('x', 13_000));
            ImpactReviewPipeline.Git(Repository, "init", "-q");
            ImpactReviewPipeline.Git(Repository, "add", ".");
            ImpactReviewPipeline.Git(Repository, "-c", "user.name=ReviewTest", "-c", "user.email=review@example.invalid", "-c", "commit.gpgsign=false", "-c", "core.hooksPath=" + Path.Combine(_root, "no-hooks"), "commit", "-qm", "fixture");
            var before = new ApiMemberModel("property:Name", "Name", "property", "public", "System.String Name [nullability:01]", false);
            var after = before with { Key = "property:Name:nullable", Signature = "System.String Name [nullability:02]" };
            ApiModel Model(string version, ApiMemberModel member) => new(1, PackageConstants.PackageId, version,
                new[] { "net8.0", "net10.0" }.Select(tfm => new FrameworkApiModel(tfm, "fixture.dll", [new("Contoso.Settings", "class", "public", null, [], [], false, [member])])).ToArray());
            _before = Model("1.0.0", before);
            _after = Model("2.0.0", noChanges ? before : after);
            if (additionKind is not null)
            {
                var baseline = Model("2.0.0", before);
                _after = baseline with
                {
                    Frameworks = baseline.Frameworks.Select(framework => framework with
                    {
                        Symbols = (additionKind is "symbol-added" or "mixed"
                            ? framework.Symbols.Append(new("Contoso.NewFeature", "class", "public", null, [], [], false, []))
                            : framework.Symbols).Select(symbol => symbol.Name == "Contoso.Settings" && (additionKind is "member-added" or "mixed")
                                ? symbol with { Members = [before, new("property:Extra", "Extra", "property", "public", "System.Int32 Extra", false)] }
                                : symbol).ToArray()
                    }).Concat(additionKind is "framework-asset-added" or "mixed"
                        ? [new FrameworkApiModel("net9.0", "fixture.dll", [])] : []).ToArray()
                };
            }
            _manifest = new() { DeclaredVersion = "2.0.0", Usages = unused ? [] : [new() { UpstreamSymbol = "Contoso.Settings", Members = ["Name"], Files = [Source], Exposure = publicExposure ? "publicly-exposed" : "internal-only" }] };
            var checks = new[] { "tool", "compare", "build", "drift-tests", "collect", "manifest", "contract-net8", "contract-net10", "boundary-net8", "boundary-net10" }.ToDictionary(name => name, _ => "success");
            if (failedCheck is not null) checks[failedCheck] = "failure";
            _checks = new(1, checks);
            if (sourceHints) OriginalCollected = new(1, PackageConstants.PackageId, "fixture.dll", [],
                [Path.Combine(Repository, Source), "/home/runner/work/original/checkout/" + Source,
                 "D:\\a\\old-checkout\\" + Source.Replace('/', '\\'), Source, "/generated/AssemblyInfo.cs", "../outside.cs"]);
            Input = Prepare();
        }

        public ImpactReviewInput Prepare() => ImpactReviewPipeline.Prepare(Repository, Workspace, ApiComparer.Compare(_before, _after), _manifest, new(), OriginalCollected, _checks, _before, _after);
        public ImpactReview Review(string verdict = "safe", string usage = "used") => new(1, Input.ReviewId, "1.0.0", "2.0.0",
            Input.Comparison.Changes.Select(change => new ImpactDecision(change.Id, verdict, usage, "Specific implementation and test reasoning.", [new(Source, Implementation), new(Test, Assertion)])).ToArray());
        public ImpactValidation Validate(ImpactReview review) => ImpactReviewPipeline.Validate(review, Input, Workspace, Repository);
        private void Write(string path, string text)
        {
            var full = Path.Combine(Repository, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, text);
        }
        public void Dispose()
        {
            if (!Paths.IsContainedBy(Path.GetTempPath(), _root) || !Path.GetFileName(_root).StartsWith("impact-review-test-", StringComparison.Ordinal))
                throw new InvalidOperationException("Unsafe temporary test path.");
            // Git object files are read-only on Windows. Do not follow the link-test directory.
            foreach (var file in Directory.EnumerateFiles(_root, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint }))
            {
                if (!Paths.IsContainedBy(_root, file)) throw new InvalidOperationException("Unsafe temporary file path.");
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            }
            Directory.Delete(_root, recursive: true);
        }
    }
}
