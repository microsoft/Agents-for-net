using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Agents.ApiCompat;
using Xunit;

namespace Microsoft.Agents.ApiCompat.Tests;

public sealed class ReportWriterTests
{
    [Fact]
    public void RenderStickyComment_EscapesMentionsAndHtml()
    {
        var report = ReportFixture.WithDetail("@team <script>|value");

        var comment = ReportWriter.RenderStickyComment(report);

        Assert.DoesNotContain("@team", comment, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", comment, StringComparison.Ordinal);
        Assert.Contains("&#64;team", comment, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderStickyComment_BoundsCommentTo60000Characters()
    {
        var report = ReportFixture.WithDetail(new string('a', 200_000));

        var comment = ReportWriter.RenderStickyComment(report);

        Assert.True(comment.Length <= 60_000, $"Comment length was {comment.Length}.");
        Assert.EndsWith("... (truncated)", comment, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderStickyComment_BoundsIndividualFieldTo2000Characters()
    {
        var report = ReportFixture.WithDetail(new string('b', 5_000));

        var comment = ReportWriter.RenderStickyComment(report);

        Assert.DoesNotContain(new string('b', 2_001), comment, StringComparison.Ordinal);
        Assert.Contains("... (truncated)", comment, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WriteAsync_WritesAllArtifactsWithSchemaVersionOne()
    {
        var report = ReportFixture.WithDetail("removed member");
        var outputDirectory = Path.Combine(AppContext.BaseDirectory, $"report-{Guid.NewGuid():N}");

        try
        {
            await ReportWriter.WriteAsync(report, outputDirectory, CancellationToken.None);

            foreach (var file in new[] { "report.json", "report.md", "summary.md", "annotations.txt", "comment.md" })
            {
                Assert.True(File.Exists(Path.Combine(outputDirectory, file)), $"Missing {file}.");
            }

            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(outputDirectory, "report.json")));
            Assert.Equal(1, document.RootElement.GetProperty("SchemaVersion").GetInt32());
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task WriteAsync_EscapesWorkflowCommandInjectionInAnnotations()
    {
        var report = ReportFixture.WithDetail("safe\n::error::injected");
        var outputDirectory = Path.Combine(AppContext.BaseDirectory, $"report-{Guid.NewGuid():N}");

        try
        {
            await ReportWriter.WriteAsync(report, outputDirectory, CancellationToken.None);

            var annotations = await File.ReadAllTextAsync(Path.Combine(outputDirectory, "annotations.txt"));

            Assert.DoesNotContain("safe\n::error::injected", annotations, StringComparison.Ordinal);
            Assert.Contains("%0A", annotations, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    private static class ReportFixture
    {
        public static CompatibilityReport WithDetail(string detail)
        {
            var finding = new CompatibilityFinding(
                "Contoso.Package",
                "1.0.0",
                "2.0.0",
                "net8.0",
                "CP0002",
                detail,
                detail,
                CompatibilityCategory.SourceAndBinary,
                FindingSeverity.Blocking);

            var package = new PackageCompatibilityReport(
                "Contoso.Package",
                "2.0.0",
                "1.0.0",
                "Breaking",
                new[] { finding });

            return new CompatibilityReport(
                1,
                987654321L,
                42,
                "main",
                AnalysisDecision.Block,
                new OverrideResult(false, null, "Missing 'breaking-change-approved' label."),
                new[] { package },
                Array.Empty<string>());
        }
    }
}

public sealed class CliCommandTests
{
    private const long RunId = 987654321L;
    private const int PullRequestNumber = 42;

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    [Theory]
    [InlineData("analyze")]
    [InlineData("analyze", "--repo-root", "root", "--packages", "pkgs", "--event", "event.json")]
    [InlineData("render-comment")]
    [InlineData("render-comment", "--report", "report.json", "--output", "comment.md", "--run-id", "1")]
    public async Task RunAsync_MissingRequiredOptions_ReturnsExitCodeTwo(params string[] args)
    {
        var exitCode = await Cli.RunAsync(args, CancellationToken.None);

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task RunAsync_UnknownCommand_ReturnsExitCodeTwo()
    {
        var exitCode = await Cli.RunAsync(new[] { "bogus" }, CancellationToken.None);

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task RunAsync_Help_ReturnsZero()
    {
        var exitCode = await Cli.RunAsync(new[] { "--help" }, CancellationToken.None);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RenderComment_ValidReport_WritesCommentAndReturnsZero()
    {
        using var report = new TemporaryReport(BuildReport());
        var output = TemporaryPath("comment.md");

        try
        {
            var exitCode = await Cli.RunAsync(
                new[] { "render-comment", "--report", report.Path, "--output", output, "--run-id", RunId.ToString(), "--pr-number", PullRequestNumber.ToString() },
                CancellationToken.None);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(output));
            Assert.Contains("API compatibility", await File.ReadAllTextAsync(output), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(output);
        }
    }

    [Fact]
    public async Task RenderComment_ReportLargerThanFiveMegabytes_Rejected()
    {
        var reportPath = TemporaryPath("report.json");
        File.WriteAllText(reportPath, new string('a', (5 * 1024 * 1024) + 1));
        var output = TemporaryPath("comment.md");

        try
        {
            var exitCode = await Cli.RunAsync(
                new[] { "render-comment", "--report", reportPath, "--output", output, "--run-id", RunId.ToString(), "--pr-number", PullRequestNumber.ToString() },
                CancellationToken.None);

            Assert.NotEqual(0, exitCode);
            Assert.False(File.Exists(output));
        }
        finally
        {
            File.Delete(reportPath);
        }
    }

    [Fact]
    public async Task RenderComment_UnsupportedSchemaVersion_Rejected()
    {
        using var report = new TemporaryReport(BuildReport() with { SchemaVersion = 2 });
        var output = TemporaryPath("comment.md");

        var exitCode = await Cli.RunAsync(
            new[] { "render-comment", "--report", report.Path, "--output", output, "--run-id", RunId.ToString(), "--pr-number", PullRequestNumber.ToString() },
            CancellationToken.None);

        Assert.NotEqual(0, exitCode);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public async Task RenderComment_RunIdMismatch_Rejected()
    {
        using var report = new TemporaryReport(BuildReport());
        var output = TemporaryPath("comment.md");

        var exitCode = await Cli.RunAsync(
            new[] { "render-comment", "--report", report.Path, "--output", output, "--run-id", "111", "--pr-number", PullRequestNumber.ToString() },
            CancellationToken.None);

        Assert.NotEqual(0, exitCode);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public async Task RenderComment_PullRequestNumberMismatch_Rejected()
    {
        using var report = new TemporaryReport(BuildReport());
        var output = TemporaryPath("comment.md");

        var exitCode = await Cli.RunAsync(
            new[] { "render-comment", "--report", report.Path, "--output", output, "--run-id", RunId.ToString(), "--pr-number", "99" },
            CancellationToken.None);

        Assert.NotEqual(0, exitCode);
        Assert.False(File.Exists(output));
    }

    [Theory]
    [MemberData(nameof(InvalidRenderReports))]
    public async Task RenderComment_MalformedSchemaValidReport_ReturnsControlledError(
        string reportJson,
        string expectedError)
    {
        using var report = new TemporaryReport(reportJson);
        var output = TemporaryPath("comment.md");

        try
        {
            var (exitCode, error) = await RunCliCapturingErrorAsync(
                new[] { "render-comment", "--report", report.Path, "--output", output, "--run-id", RunId.ToString(), "--pr-number", PullRequestNumber.ToString() });

            Assert.Equal(1, exitCode);
            Assert.False(File.Exists(output));
            Assert.Contains(expectedError, error, StringComparison.Ordinal);
            Assert.DoesNotContain("NullReferenceException", error, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(output);
        }
    }

    public static TheoryData<string, string> InvalidRenderReports =>
        new()
        {
            { BuildReportJson(baseRefJson: "\"\""), "Report BaseRef must be non-empty." },
            { BuildReportJson(overrideJson: "null"), "Report Override is required." },
            { BuildReportJson(packagesJson: "null"), "Report Packages is required." },
            { BuildReportJson(infrastructureErrorsJson: "null"), "Report InfrastructureErrors is required." },
            {
                BuildReportJson(packagesJson: """[{"PackageId":"","CandidateVersion":"2.0.0","BaselineVersion":"1.0.0","Status":"Breaking","Findings":[]}]"""),
                "Report Packages[0].PackageId must be non-empty."
            },
            {
                BuildReportJson(packagesJson: """[{"PackageId":"Contoso.Package","CandidateVersion":"2.0.0","BaselineVersion":"1.0.0","Status":"Breaking","Findings":null}]"""),
                "Report Packages[0].Findings is required."
            },
            {
                BuildReportJson(
                    packagesJson: """[{"PackageId":"Contoso.Package","CandidateVersion":"2.0.0","BaselineVersion":"1.0.0","Status":"Breaking","Findings":[{"PackageId":"Contoso.Package","BaselineVersion":"1.0.0","CandidateVersion":"2.0.0","TargetFramework":"net8.0","DiagnosticId":"CP0002","Target":"Api.Removed","Detail":"","Category":"SourceAndBinary","Severity":"Blocking"}]}]"""),
                "Report Packages[0].Findings[0].Detail must be non-empty."
            },
            { BuildReportJson(infrastructureErrorsJson: """[""]"""), "Report InfrastructureErrors[0] must be non-empty." },
        };

    private static CompatibilityReport BuildReport()
    {
        return new CompatibilityReport(
            SchemaVersion: 1,
            RunId: RunId,
            PullRequestNumber: PullRequestNumber,
            BaseRef: "main",
            Decision: AnalysisDecision.Pass,
            Override: new OverrideResult(false, null, "No override."),
            Packages: Array.Empty<PackageCompatibilityReport>(),
            InfrastructureErrors: Array.Empty<string>());
    }

    private static string BuildReportJson(
        string baseRefJson = "\"main\"",
        string overrideJson = """{"IsValid":false,"Justification":null,"Reason":"No override."}""",
        string packagesJson = "[]",
        string infrastructureErrorsJson = "[]") =>
        $$"""
        {
          "SchemaVersion": 1,
          "RunId": {{RunId}},
          "PullRequestNumber": {{PullRequestNumber}},
          "BaseRef": {{baseRefJson}},
          "Decision": "Pass",
          "Override": {{overrideJson}},
          "Packages": {{packagesJson}},
          "InfrastructureErrors": {{infrastructureErrorsJson}}
        }
        """;

    private static async Task<(int ExitCode, string Error)> RunCliCapturingErrorAsync(string[] args)
    {
        var original = Console.Error;
        using var writer = new StringWriter();
        Console.SetError(writer);

        try
        {
            var exitCode = await Cli.RunAsync(args, CancellationToken.None);
            return (exitCode, writer.ToString());
        }
        finally
        {
            Console.SetError(original);
        }
    }

    private static string TemporaryPath(string suffix) =>
        Path.Combine(AppContext.BaseDirectory, $"cli-{Guid.NewGuid():N}-{suffix}");

    private sealed class TemporaryReport : IDisposable
    {
        public TemporaryReport(CompatibilityReport report)
        {
            Path = TemporaryPath("report.json");
            File.WriteAllText(Path, JsonSerializer.Serialize(report, WriteOptions));
        }

        public TemporaryReport(string reportJson)
        {
            Path = TemporaryPath("report.json");
            File.WriteAllText(Path, reportJson);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (File.Exists(Path))
            {
                File.Delete(Path);
            }
        }
    }
}
