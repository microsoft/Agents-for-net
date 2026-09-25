#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Core.Models;

public sealed class ActivityLogExporterTests
{
    [Theory]
    [InlineData("1234567890extra", "1234567890")]
    [InlineData(null, "NO-ID")]
    [InlineData("", "NO-ID")]
    [InlineData("         ", "NO-ID")]
    [InlineData("short", "NO-ID")]
    [InlineData("A:b/c\\d?*e", "A-b-c-d--e")]
    [InlineData("          extra", "NO-ID")]
    public void CreatePrefix_UsesPortableFirstTenCharacters(string? conversationId, string expected)
    {
        Assert.Equal(expected, ActivityLogExporter.CreatePrefix(conversationId));
    }

    [Fact]
    public async Task ExportAsync_UsesExactUtcTimestampAndReturnsFilenameOnly()
    {
        using TempDirectory directory = new();
        ActivityLogExporter exporter = CreateExporter(directory);

        string filename = await exporter.ExportAsync(
            new ActivityJournal(),
            "abcdefghij-extra",
            CancellationToken.None);

        Assert.Equal("abcdefghij-20260917-225547-516.json", filename);
        Assert.True(File.Exists(Path.Combine(directory.Path, filename)));
        Assert.Equal("[]", await File.ReadAllTextAsync(Path.Combine(directory.Path, filename)));
    }

    [Fact]
    public async Task ExportAsync_WritesIndentedActivityObjectArrayInJournalOrderWithoutDiagnostics()
    {
        using TempDirectory directory = new();
        ActivityJournal journal = new();
        journal.Append(
            new Activity { Type = ActivityTypes.Message, Text = "first" },
            ActivityDirection.Outbound);
        journal.AppendDiagnostic("not part of the export", DiagnosticSeverity.Warning);
        journal.Append(
            new Activity { Type = ActivityTypes.Event, Name = "second" },
            ActivityDirection.Inbound);

        string filename = await CreateExporter(directory).ExportAsync(
            journal,
            "conversation-id",
            CancellationToken.None);
        string json = await File.ReadAllTextAsync(Path.Combine(directory.Path, filename));

        Assert.Contains(Environment.NewLine, json, StringComparison.Ordinal);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement[] activities = document.RootElement.EnumerateArray().ToArray();
        Assert.Equal(2, activities.Length);
        Assert.Equal("first", activities[0].GetProperty("text").GetString());
        Assert.Equal("second", activities[1].GetProperty("name").GetString());
        Assert.DoesNotContain("not part of the export", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportAsync_CollisionUsesSuffixWithoutOverwritingExistingFile()
    {
        using TempDirectory directory = new();
        ActivityLogExporter exporter = CreateExporter(directory);
        string originalPath = Path.Combine(
            directory.Path,
            "abcdefghij-20260917-225547-516.json");
        await File.WriteAllTextAsync(originalPath, "original");

        string filename = await exporter.ExportAsync(
            new ActivityJournal(),
            "abcdefghij-extra",
            CancellationToken.None);

        Assert.Equal("abcdefghij-20260917-225547-516-1.json", filename);
        Assert.Equal("original", await File.ReadAllTextAsync(originalPath));
        Assert.Equal("[]", await File.ReadAllTextAsync(Path.Combine(directory.Path, filename)));
    }

    [Fact]
    public async Task ExportAsync_InvalidStoredJsonDeletesTemporaryFileWithoutLeakingPayload()
    {
        const string secret = "activity-payload-must-not-leak";
        using TempDirectory directory = new();
        ActivityJournal journal = new(
            _ => $"{{\"type\":\"message\",\"text\":\"{secret}");
        journal.Append(new Activity { Type = ActivityTypes.Message }, ActivityDirection.Inbound);

        JsonException exception = await Assert.ThrowsAnyAsync<JsonException>(
            () => CreateExporter(directory).ExportAsync(
                journal,
                "abcdefghij-extra",
                CancellationToken.None));

        Assert.DoesNotContain(secret, exception.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFileSystemEntries(directory.Path));
    }

    [Fact]
    public async Task ExportAsync_WriteFailureDeletesTemporaryFileWithoutLeakingPayload()
    {
        const string secret = "activity-payload-must-not-leak";
        using TempDirectory directory = new();
        ActivityJournal journal = new();
        journal.Append(
            new Activity { Type = ActivityTypes.Message, Text = secret },
            ActivityDirection.Inbound);
        ActivityLogExporter exporter = new(
            utcNow: FixedUtcNow,
            currentDirectory: () => directory.Path,
            createTempFile: path => new FailingWriteStream(path));

        IOException exception = await Assert.ThrowsAsync<IOException>(
            () => exporter.ExportAsync(
                journal,
                "abcdefghij-extra",
                CancellationToken.None));

        Assert.DoesNotContain(secret, exception.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFileSystemEntries(directory.Path));
    }

    private static ActivityLogExporter CreateExporter(TempDirectory directory)
    {
        return new ActivityLogExporter(
            utcNow: FixedUtcNow,
            currentDirectory: () => directory.Path);
    }

    private static DateTimeOffset FixedUtcNow()
    {
        return new DateTimeOffset(2026, 9, 17, 22, 55, 47, 516, TimeSpan.Zero);
    }

    private sealed class TempDirectory : IDisposable
    {
        internal TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"agents-terminal-export-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        internal string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    private sealed class FailingWriteStream : Stream
    {
        private readonly FileStream _inner;

        internal FailingWriteStream(string path)
        {
            _inner = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => _inner.Length;

        public override long Position
        {
            get => _inner.Position;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
            throw new IOException("simulated write failure");
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            _inner.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new IOException("simulated write failure");
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            throw new IOException("simulated write failure");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
