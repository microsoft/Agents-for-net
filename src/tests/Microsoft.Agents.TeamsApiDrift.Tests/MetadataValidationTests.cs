using System.Diagnostics;
using Microsoft.Agents.TeamsApiDrift;
using Xunit;

namespace Microsoft.Agents.TeamsApiDrift.Tests;

public sealed class MetadataValidationTests
{
    [Fact]
    public void CurrentMetadataMatchesBuiltUsage()
    {
        using var fixture = new MetadataFixture();

        Assert.True(fixture.Validate().Valid);
    }

    [Fact]
    public void CurrentStateReportsVersionSymbolMemberExposurePathReviewAndCapabilityErrors()
    {
        AssertFinding(fixture => fixture.Manifest.DeclaredVersion = "1.0.0", MetadataValidator.UsageStaleRule, "does not match Directory.Packages.props");
        AssertFinding(fixture => fixture.Manifest.Usages[0].UpstreamSymbol = "Microsoft.Teams.Apps.Schema.Other", MetadataValidator.UsageStaleRule, "does not reference");
        AssertFinding(fixture => fixture.Manifest.Usages[0].Members = ["Other"], MetadataValidator.UsageStaleRule, "member usage is missing");
        AssertFinding(fixture => fixture.Manifest.Usages[0].Exposure = "publicly-exposed", MetadataValidator.UsageStaleRule, "exposure in the built extension");
        AssertFinding(fixture => fixture.Manifest.Usages[0].Files = ["../outside.cs"], MetadataValidator.UsageStaleRule, "missing or unsafe source file");
        AssertFinding(fixture => fixture.Manifest.SourceReview = new SourceReview { Outcome = MetadataValidator.UsageReviewOutcome, Reason = "" }, MetadataValidator.UsageStaleRule, "non-empty reason");
        AssertFinding(fixture => fixture.Capabilities.Capabilities["schema"].UpstreamNamespaces = ["Microsoft.Teams.Apps.Other"], MetadataValidator.CapabilitiesStaleRule, "No capability maps");
    }

    [Fact]
    public void SourceReviewCannotSuppressProvableMismatch()
    {
        using var fixture = new MetadataFixture();
        fixture.Manifest.DeclaredVersion = "1.0.0";
        fixture.Manifest.SourceReview = UsageReview("The dependency version is intentionally unchanged.");

        var result = fixture.Validate();

        Assert.Contains(result.Findings, finding => finding.RuleId == MetadataValidator.UsageStaleRule && finding.Message.Contains("does not match", StringComparison.Ordinal));
    }

    [Fact]
    public void RelevantWorkingSourceChangeRequiresUsageReview()
    {
        using var fixture = new MetadataFixture(git: true);
        fixture.AppendSource("// implementation change");

        var result = fixture.Validate(fixture.BaseCommit);

        Assert.Contains(result.Findings, finding => finding.RuleId == MetadataValidator.UsageReviewRule);
    }

    [Fact]
    public void SubstantiveUsageUpdateOrFreshAcknowledgmentSatisfiesSourceReview()
    {
        using var substantive = new MetadataFixture(git: true);
        substantive.AppendSource("// implementation change");
        substantive.Manifest.Usages[0].UsageKinds.Add("updated-routing");
        substantive.WriteManifest();
        Assert.DoesNotContain(substantive.Validate(substantive.BaseCommit).Findings, finding => finding.RuleId == MetadataValidator.UsageReviewRule);

        using var acknowledged = new MetadataFixture(git: true);
        acknowledged.AppendSource("// implementation change");
        acknowledged.Manifest.SourceReview = UsageReview("Only routing implementation details changed.");
        acknowledged.WriteManifest();
        Assert.DoesNotContain(acknowledged.Validate(acknowledged.BaseCommit).Findings, finding => finding.RuleId == MetadataValidator.UsageReviewRule);
    }

    [Fact]
    public void BlankUnchangedAndWrongDocumentReviewsDoNotSatisfyUsageReview()
    {
        using var blank = new MetadataFixture(git: true);
        blank.AppendSource("// implementation change");
        blank.Manifest.SourceReview = UsageReview("");
        blank.WriteManifest();
        Assert.Contains(blank.Validate(blank.BaseCommit).Findings, finding => finding.RuleId == MetadataValidator.UsageReviewRule);

        using var unchanged = new MetadataFixture();
        unchanged.Manifest.SourceReview = UsageReview("Reviewed before this change.");
        unchanged.WriteManifest();
        unchanged.InitializeGit();
        unchanged.AppendSource("// implementation change");
        Assert.Contains(unchanged.Validate(unchanged.BaseCommit).Findings, finding => finding.RuleId == MetadataValidator.UsageReviewRule);

        using var wrongDocument = new MetadataFixture(git: true);
        wrongDocument.AppendSource("// implementation change");
        wrongDocument.Capabilities.SourceReview = CapabilityReview("Usage metadata is unaffected.");
        wrongDocument.WriteCapabilities();
        Assert.Contains(wrongDocument.Validate(wrongDocument.BaseCommit).Findings, finding => finding.RuleId == MetadataValidator.UsageReviewRule);
    }

    [Theory]
    [InlineData("staged")]
    [InlineData("unstaged")]
    [InlineData("untracked")]
    [InlineData("deleted")]
    [InlineData("committed")]
    public void GitChangeCollectionIncludesAllRelevantStates(string state)
    {
        using var fixture = new MetadataFixture(git: true);
        switch (state)
        {
            case "staged":
                fixture.AppendSource("// staged");
                fixture.Git("add", fixture.SourceRelativePath);
                break;
            case "unstaged":
                fixture.AppendSource("// unstaged");
                break;
            case "untracked":
                fixture.WriteSource("NewUsage.cs", "public class NewUsage { Microsoft.Teams.Apps.Schema.Used? Value { get; set; } }");
                break;
            case "deleted":
                File.Delete(fixture.SourcePath);
                break;
            case "committed":
                fixture.AppendSource("// committed");
                fixture.Commit("change source");
                break;
        }

        Assert.Contains(fixture.Validate(fixture.BaseCommit).Findings, finding => finding.RuleId == MetadataValidator.UsageReviewRule);
    }

    [Fact]
    public void AcknowledgmentMustBeAtLeastAsRecentAsSourceChange()
    {
        using var fresh = new MetadataFixture(git: true);
        fresh.AppendSource("// source first");
        fresh.Commit("change source");
        fresh.Manifest.SourceReview = UsageReview("No usage metadata changed.");
        fresh.WriteManifest();
        fresh.Commit("review usage metadata");
        Assert.DoesNotContain(fresh.Validate(fresh.BaseCommit).Findings, finding => finding.RuleId == MetadataValidator.UsageReviewRule);

        using var stale = new MetadataFixture(git: true);
        stale.Manifest.SourceReview = UsageReview("No usage metadata changed.");
        stale.WriteManifest();
        stale.Commit("review usage metadata");
        stale.AppendSource("// source after review");
        stale.Commit("change source");
        Assert.Contains(stale.Validate(stale.BaseCommit).Findings, finding => finding.RuleId == MetadataValidator.UsageReviewRule);
    }

    [Fact]
    public void ChangedUsageSymbolsRequireTargetedCapabilityUpdateOrReview()
    {
        using var reviewed = new MetadataFixture(git: true);
        reviewed.ChangeSymbol("Microsoft.Teams.Apps.Schema.Other");
        reviewed.Capabilities.SourceReview = CapabilityReview("The existing schema capability still owns this symbol.");
        reviewed.WriteCapabilities();
        var reviewedResult = reviewed.Validate(reviewed.BaseCommit);
        Assert.DoesNotContain(reviewedResult.Findings, finding => finding.RuleId == MetadataValidator.CapabilitiesReviewRule);

        using var targeted = new MetadataFixture(git: true);
        targeted.ChangeSymbol("Microsoft.Teams.Apps.NewArea.Other");
        targeted.Capabilities.Capabilities["schema"].UpstreamNamespaces = ["Microsoft.Teams.Apps.NewArea"];
        targeted.WriteCapabilities();
        var targetedResult = targeted.Validate(targeted.BaseCommit);
        Assert.DoesNotContain(targetedResult.Findings, finding => finding.RuleId == MetadataValidator.CapabilitiesReviewRule);

        using var missing = new MetadataFixture(git: true);
        missing.ChangeSymbol("Microsoft.Teams.Apps.Schema.Other");
        Assert.Contains(missing.Validate(missing.BaseCommit).Findings, finding => finding.RuleId == MetadataValidator.CapabilitiesReviewRule);
    }

    [Fact]
    public void ExplicitInvalidBaseRefIsAConfigurationError()
    {
        using var fixture = new MetadataFixture(git: true);

        var error = Assert.Throws<InvalidOperationException>(() => fixture.Validate("missing-base-ref"));

        Assert.Contains("Unable to resolve Git base ref", error.Message, StringComparison.Ordinal);
    }

    private static void AssertFinding(Action<MetadataFixture> mutate, string rule, string message)
    {
        using var fixture = new MetadataFixture();
        mutate(fixture);
        var result = fixture.Validate();
        Assert.Contains(result.Findings, finding => finding.RuleId == rule && finding.Message.Contains(message, StringComparison.Ordinal));
    }

    private static SourceReview UsageReview(string reason) => new() { Outcome = MetadataValidator.UsageReviewOutcome, Reason = reason };
    private static SourceReview CapabilityReview(string reason) => new() { Outcome = MetadataValidator.CapabilitiesReviewOutcome, Reason = reason };

    private sealed class MetadataFixture : IDisposable
    {
        private const string Symbol = "Microsoft.Teams.Apps.Schema.Used";
        private readonly string _manifestPath;
        private readonly string _capabilitiesPath;

        public MetadataFixture(bool git = false)
        {
            Root = Path.Combine(Path.GetTempPath(), $"teams-metadata-{Guid.NewGuid():N}");
            SourceRelativePath = Paths.Normalize(Path.Combine(PackageConstants.SourceRoot, "TeamsUsage.cs"));
            SourcePath = Path.Combine(Root, SourceRelativePath);
            _manifestPath = Path.Combine(Root, "scripts", "TeamsApiDrift", "teams-api-usage.json");
            _capabilitiesPath = Path.Combine(Root, "scripts", "TeamsApiDrift", "teams-capabilities.json");
            Directory.CreateDirectory(Path.GetDirectoryName(SourcePath)!);
            File.WriteAllText(SourcePath, $"public class TeamsUsage {{ {Symbol}? Value {{ get; set; }} }}{Environment.NewLine}");
            Manifest = new UsageManifest
            {
                DeclaredVersion = "2.1.0",
                Usages =
                [
                    new UsageEntry
                    {
                        UpstreamSymbol = Symbol,
                        Members = ["Run"],
                        UsageKinds = ["routing"],
                        Exposure = "internal-only",
                        Files = [SourceRelativePath]
                    }
                ]
            };
            Capabilities = new CapabilityDocument
            {
                Capabilities = new Dictionary<string, Capability>
                {
                    ["schema"] = new()
                    {
                        Owners = ["agents-sdk-msteams"],
                        UpstreamNamespaces = ["Microsoft.Teams.Apps.Schema"],
                        AdoptionPolicy = "review-new-members"
                    }
                }
            };
            Collected = new CollectedUsage(1, PackageConstants.PackageId, "fixture.dll", [new(Symbol, ["Run"], "internal-only")]);
            WriteManifest();
            WriteCapabilities();
            if (git) InitializeGit();
        }

        public string Root { get; }
        public string SourcePath { get; }
        public string SourceRelativePath { get; }
        public string BaseCommit { get; private set; } = string.Empty;
        public UsageManifest Manifest { get; }
        public CapabilityDocument Capabilities { get; }
        public CollectedUsage Collected { get; private set; }

        public MetadataValidation Validate(string? baseRef = null)
            => MetadataValidator.Validate(Manifest, Capabilities, Collected, "2.1.0", Root, _manifestPath, _capabilitiesPath, baseRef);

        public void InitializeGit()
        {
            Git("init");
            Git("config", "user.email", "teams-api-drift@example.com");
            Git("config", "user.name", "Teams API Drift Tests");
            Commit("initial metadata");
            BaseCommit = Git("rev-parse", "HEAD").Trim();
        }

        public void AppendSource(string text) => File.AppendAllText(SourcePath, text + Environment.NewLine);

        public void WriteSource(string name, string text)
        {
            var path = Path.Combine(Root, PackageConstants.SourceRoot, name);
            File.WriteAllText(path, text + Environment.NewLine);
        }

        public void ChangeSymbol(string symbol)
        {
            File.WriteAllText(SourcePath, $"public class TeamsUsage {{ {symbol}? Value {{ get; set; }} }}{Environment.NewLine}");
            Manifest.Usages[0].UpstreamSymbol = symbol;
            Collected = new CollectedUsage(1, PackageConstants.PackageId, "fixture.dll", [new(symbol, ["Run"], "internal-only")]);
            WriteManifest();
        }

        public void WriteManifest() => ToolJson.Write(_manifestPath, Manifest);
        public void WriteCapabilities() => ToolJson.Write(_capabilitiesPath, Capabilities);

        public void Commit(string message)
        {
            Git("add", ".");
            Git("commit", "-m", message);
        }

        public string Git(params string[] arguments)
        {
            var info = new ProcessStartInfo("git")
            {
                WorkingDirectory = Root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var argument in arguments) info.ArgumentList.Add(argument);
            using var process = Process.Start(info) ?? throw new InvalidOperationException("Unable to start Git.");
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0) throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {error}");
            return output;
        }

        public void Dispose()
        {
            foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }
            Directory.Delete(Root, recursive: true);
        }
    }
}
