using Microsoft.Agents.ApiCompat;
using Xunit;

namespace Microsoft.Agents.ApiCompat.Tests;

public sealed class ApiCompatParserTests(ApiCompatParserTests.ApiCompatPackageFixture fixture) : IClassFixture<ApiCompatParserTests.ApiCompatPackageFixture>
{
    [Fact]
    public async Task RunAsync_NormalRun_MatchesPinnedOutputAndParsesRemovals()
    {
        var execution = await ApiCompatRunner.RunAsync(fixture.CandidatePackage, fixture.BaselinePackage, strict: false, CancellationToken.None);

        Assert.True(execution.ExitCode == 0, DescribeExecution(execution));
        Assert.Equal(string.Empty, Normalize(execution.StandardOutput));
        Assert.Equal(ExpectedNormalOutput(), Normalize(execution.StandardError));
        Assert.Collection(
            ApiCompatParser.Parse(execution, strict: false),
            diagnostic => Assert.Equal(
                new ParsedDiagnostic(
                    "CP0002",
                    "void Fixture.Api.Removed()",
                    "Member 'void Fixture.Api.Removed()' exists on [Baseline] lib/net8.0/Fixture.dll but not on lib/net8.0/Fixture.dll",
                    "net8.0",
                    ApiDifferenceDirection.BaselineToCandidate),
                diagnostic),
            diagnostic => Assert.Equal(
                new ParsedDiagnostic(
                    "CP0017",
                    "Fixture.Api.Named(int)",
                    "Parameter name on member 'Fixture.Api.Named(int)' changed from 'value' to 'renamed'.",
                    null,
                    ApiDifferenceDirection.BaselineToCandidate),
                diagnostic));
    }

    [Fact]
    public async Task RunAsync_StrictRun_MatchesPinnedOutputAndParsesCandidateAddition()
    {
        var execution = await ApiCompatRunner.RunAsync(fixture.CandidatePackage, fixture.BaselinePackage, strict: true, CancellationToken.None);

        Assert.True(execution.ExitCode == 0, DescribeExecution(execution));
        Assert.Equal(string.Empty, Normalize(execution.StandardOutput));
        Assert.Equal(ExpectedStrictOutput(), Normalize(execution.StandardError));
        Assert.Collection(
            ApiCompatParser.Parse(execution, strict: true),
            diagnostic => Assert.Equal(
                new ParsedDiagnostic(
                    "CP0002",
                    "void Fixture.Api.Removed()",
                    "Member 'void Fixture.Api.Removed()' exists on [Baseline] lib/net8.0/Fixture.dll but not on lib/net8.0/Fixture.dll",
                    "net8.0",
                    ApiDifferenceDirection.BaselineToCandidate),
                diagnostic),
            diagnostic => Assert.Equal(
                new ParsedDiagnostic(
                    "CP0017",
                    "Fixture.Api.Named(int)",
                    "Parameter name on member 'Fixture.Api.Named(int)' changed from 'value' to 'renamed'.",
                    null,
                    ApiDifferenceDirection.BaselineToCandidate),
                diagnostic),
            diagnostic => Assert.Equal(
                new ParsedDiagnostic(
                    "CP0002",
                    "void Fixture.Api.Added(string)",
                    "Member 'void Fixture.Api.Added(string)' exists on lib/net8.0/Fixture.dll but not on [Baseline] lib/net8.0/Fixture.dll",
                    "net8.0",
                    ApiDifferenceDirection.CandidateAddition),
                diagnostic));
    }

    [Fact]
    public async Task RunAsync_StrictRun_WithPublicVisibilityExpansion_MatchesPinnedOutputAndParsesCandidateAddition()
    {
        var execution = await ApiCompatRunner.RunAsync(
            fixture.CandidateVisibilityExpansionPackage,
            fixture.BaselineVisibilityExpansionPackage,
            strict: true,
            CancellationToken.None);

        Assert.True(execution.ExitCode == 0, DescribeExecution(execution));
        Assert.Equal(string.Empty, Normalize(execution.StandardOutput));
        Assert.Equal(ExpectedStrictVisibilityExpansionOutput(), Normalize(execution.StandardError));
        Assert.Collection(
            ApiCompatParser.Parse(execution, strict: true),
            diagnostic => Assert.Equal(
                new ParsedDiagnostic(
                    "CP0020",
                    "Fixture.Api.Widened()",
                    "Visibility of 'Fixture.Api.Widened()' expanded from 'Protected' to 'Public'.",
                    null,
                    ApiDifferenceDirection.CandidateAddition),
                diagnostic));
    }

    [Fact]
    public async Task RunAsync_NoBreakingChanges_ParsesEmptyResult()
    {
        var execution = await ApiCompatRunner.RunAsync(fixture.CandidatePackage, fixture.CandidatePackage, strict: false, CancellationToken.None);

        Assert.True(execution.ExitCode == 0, DescribeExecution(execution));
        Assert.Equal("APICompat ran successfully without finding any breaking changes.", Normalize(execution.StandardOutput));
        Assert.Equal(string.Empty, Normalize(execution.StandardError));
        Assert.Empty(ApiCompatParser.Parse(execution, strict: false));
    }

    [Fact]
    public void Parse_DeduplicatesRepeatedDiagnostics()
    {
        var detail = "CP0002: Member 'void Fixture.Api.Removed()' exists on [Baseline] lib/net8.0/Fixture.dll but not on lib/net8.0/Fixture.dll";
        var execution = new ApiCompatExecution(0, $"{detail}\r\n{detail}", string.Empty);

        var diagnostics = ApiCompatParser.Parse(execution, strict: false);

        Assert.Single(diagnostics);
    }

    [Theory]
    [InlineData(
        false,
        "CP0001: Type 'Fixture.MissingType' exists on [Baseline] lib/net8.0/Fixture.dll but not on lib/net8.0/Fixture.dll",
        "CP0001",
        "Fixture.MissingType",
        "net8.0",
        ApiDifferenceDirection.BaselineToCandidate)]
    [InlineData(
        false,
        "CP0003: [Baseline] assembly version '1.0.0.0' should be equal to lib/net8.0/Fixture.dll version '2.0.0.0'.",
        "CP0003",
        "assembly version",
        "net8.0",
        ApiDifferenceDirection.BaselineToCandidate)]
    [InlineData(
        true,
        "CP0006: Cannot add interface member 'void Fixture.IContract.Added()' to lib/net8.0/Fixture.dll because it does not exist on [Baseline] lib/net8.0/Fixture.dll",
        "CP0006",
        "void Fixture.IContract.Added()",
        "net8.0",
        ApiDifferenceDirection.CandidateAddition)]
    [InlineData(
        false,
        "PKV006: Target framework .NETStandard,Version=v2.0 is no longer supported in the latest version.",
        "PKV006",
        ".NETStandard,Version=v2.0",
        ".NETStandard,Version=v2.0",
        ApiDifferenceDirection.BaselineToCandidate)]
    public void Parse_SupportedDiagnosticShapes_ParsesKnownIds(
        bool strict,
        string line,
        string id,
        string target,
        string? targetFramework,
        ApiDifferenceDirection direction)
    {
        var execution = new ApiCompatExecution(0, line, string.Empty);

        var diagnostics = ApiCompatParser.Parse(execution, strict);

        Assert.Collection(
            diagnostics,
            diagnostic =>
            {
                Assert.Equal(id, diagnostic.Id);
                Assert.Equal(target, diagnostic.Target);
                Assert.Equal(line[(line.IndexOf(':', StringComparison.Ordinal) + 1)..].Trim(), diagnostic.Detail);
                Assert.Equal(targetFramework, diagnostic.TargetFramework);
                Assert.Equal(direction, diagnostic.Direction);
            });
    }

    [Fact]
    public void Parse_ThrowsForUnparseableDiagnosticShapeEvenWhenExitCodeIsZero()
    {
        var execution = new ApiCompatExecution(
            0,
            "CP0002: Member 'void Fixture.Api.Removed()' unexpectedly vanished.",
            string.Empty);

        var exception = Assert.Throws<InvalidDataException>(() => ApiCompatParser.Parse(execution, strict: false));

        Assert.Contains("unparseable", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_ThrowsForUnsupportedDiagnosticId()
    {
        var execution = new ApiCompatExecution(
            0,
            "CP9999: Type 'Fixture.MissingType' exists on [Baseline] lib/net8.0/Fixture.dll but not on lib/net8.0/Fixture.dll",
            string.Empty);

        var exception = Assert.Throws<InvalidDataException>(() => ApiCompatParser.Parse(execution, strict: false));

        Assert.Contains("CP9999", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_ThrowsForCandidateOnlyDiagnosticWhenNotStrict()
    {
        var execution = new ApiCompatExecution(
            0,
            "CP0002: Member 'void Fixture.Api.Added(string)' exists on lib/net8.0/Fixture.dll but not on [Baseline] lib/net8.0/Fixture.dll",
            string.Empty);

        var exception = Assert.Throws<InvalidDataException>(() => ApiCompatParser.Parse(execution, strict: false));

        Assert.Contains("candidate-only", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_ThrowsForPublicVisibilityExpansionWhenNotStrict()
    {
        var execution = new ApiCompatExecution(
            0,
            "CP0020: Visibility of 'Fixture.Api.Widened()' expanded from 'Protected' to 'Public'.",
            string.Empty);

        var exception = Assert.Throws<InvalidDataException>(() => ApiCompatParser.Parse(execution, strict: false));

        Assert.Contains("candidate-only", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_ThrowsForAmbiguousVisibilityExpansionShape()
    {
        var execution = new ApiCompatExecution(
            0,
            "CP0020: Visibility of 'Fixture.Api.Widened()' expanded from 'Protected' to 'Protected Internal'.",
            string.Empty);

        var exception = Assert.Throws<InvalidDataException>(() => ApiCompatParser.Parse(execution, strict: true));

        Assert.Contains("unparseable", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_ThrowsForUnparseableNonZeroOutput()
    {
        var execution = new ApiCompatExecution(7, string.Empty, "fatal failure");

        var exception = Assert.Throws<InvalidDataException>(() => ApiCompatParser.Parse(execution, strict: false));

        Assert.Contains("fatal failure", exception.Message, StringComparison.Ordinal);
    }

    private string ExpectedNormalOutput() =>
        Normalize($$"""
        API compatibility errors between 'lib/net8.0/Fixture.dll' ({{fixture.BaselinePackage}}) and 'lib/net8.0/Fixture.dll' ({{fixture.CandidatePackage}}):
        CP0002: Member 'void Fixture.Api.Removed()' exists on [Baseline] lib/net8.0/Fixture.dll but not on lib/net8.0/Fixture.dll
        CP0017: Parameter name on member 'Fixture.Api.Named(int)' changed from 'value' to 'renamed'.
        API breaking changes found. If those are intentional, the APICompat suppression file can be updated by specifying the '--generate-suppression-file' parameter.
        """);

    private string ExpectedStrictOutput() =>
        Normalize($$"""
        API compatibility errors between 'lib/net8.0/Fixture.dll' ({{fixture.BaselinePackage}}) and 'lib/net8.0/Fixture.dll' ({{fixture.CandidatePackage}}):
        CP0002: Member 'void Fixture.Api.Removed()' exists on [Baseline] lib/net8.0/Fixture.dll but not on lib/net8.0/Fixture.dll
        CP0017: Parameter name on member 'Fixture.Api.Named(int)' changed from 'value' to 'renamed'.
        CP0002: Member 'void Fixture.Api.Added(string)' exists on lib/net8.0/Fixture.dll but not on [Baseline] lib/net8.0/Fixture.dll
        API breaking changes found. If those are intentional, the APICompat suppression file can be updated by specifying the '--generate-suppression-file' parameter.
        """);

    private static string Normalize(string value) => value.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n', '\r');

    private string ExpectedStrictVisibilityExpansionOutput() =>
        Normalize($$"""
        API compatibility errors between 'lib/net8.0/Fixture.dll' ({{fixture.BaselineVisibilityExpansionPackage}}) and 'lib/net8.0/Fixture.dll' ({{fixture.CandidateVisibilityExpansionPackage}}):
        CP0020: Visibility of 'Fixture.Api.Widened()' expanded from 'Protected' to 'Public'.
        API breaking changes found. If those are intentional, the APICompat suppression file can be updated by specifying the '--generate-suppression-file' parameter.
        """);

    private static string DescribeExecution(ApiCompatExecution execution) =>
        $"""
        ExitCode: {execution.ExitCode}
        STDOUT:
        {execution.StandardOutput}
        STDERR:
        {execution.StandardError}
        """;

    public sealed class ApiCompatPackageFixture : IAsyncLifetime
    {
        private readonly List<string> _roots = [];

        public string BaselinePackage { get; private set; } = string.Empty;

        public string CandidatePackage { get; private set; } = string.Empty;

        public string BaselineVisibilityExpansionPackage { get; private set; } = string.Empty;

        public string CandidateVisibilityExpansionPackage { get; private set; } = string.Empty;

        public async Task InitializeAsync()
        {
            const string baselineSource = "namespace Fixture; public class Api { public void Removed() { } public void Named(int value) { } }";
            const string candidateSource = "namespace Fixture; public class Api { public void Named(int renamed) { } public void Added(string value) { } }";
            const string baselineVisibilityExpansionSource = "namespace Fixture; public class Api { protected void Widened() { } }";
            const string candidateVisibilityExpansionSource = "namespace Fixture; public class Api { public void Widened() { } }";

            BaselinePackage = await TestPackageBuilder.BuildAsync("Fixture.Baseline", "1.0.0", baselineSource);
            CandidatePackage = await TestPackageBuilder.BuildAsync("Fixture.Candidate", "1.0.0", candidateSource);
            BaselineVisibilityExpansionPackage = await TestPackageBuilder.BuildAsync(
                "Fixture.Visibility.Baseline",
                "1.0.0",
                baselineVisibilityExpansionSource);
            CandidateVisibilityExpansionPackage = await TestPackageBuilder.BuildAsync(
                "Fixture.Visibility.Candidate",
                "1.0.0",
                candidateVisibilityExpansionSource);

            _roots.Add(Path.GetDirectoryName(BaselinePackage)!);
            _roots.Add(Path.GetDirectoryName(CandidatePackage)!);
            _roots.Add(Path.GetDirectoryName(BaselineVisibilityExpansionPackage)!);
            _roots.Add(Path.GetDirectoryName(CandidateVisibilityExpansionPackage)!);
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
    }
}
