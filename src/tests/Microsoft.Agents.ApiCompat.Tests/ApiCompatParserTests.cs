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

        public async Task InitializeAsync()
        {
            const string baselineSource = "namespace Fixture; public class Api { public void Removed() { } public void Named(int value) { } }";
            const string candidateSource = "namespace Fixture; public class Api { public void Named(int renamed) { } public void Added(string value) { } }";

            BaselinePackage = await TestPackageBuilder.BuildAsync("Fixture.Baseline", "1.0.0", baselineSource);
            CandidatePackage = await TestPackageBuilder.BuildAsync("Fixture.Candidate", "1.0.0", candidateSource);

            _roots.Add(Path.GetDirectoryName(BaselinePackage)!);
            _roots.Add(Path.GetDirectoryName(CandidatePackage)!);
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
