using System.Text.Json;
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
