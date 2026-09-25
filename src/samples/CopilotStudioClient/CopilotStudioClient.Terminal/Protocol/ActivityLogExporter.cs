#nullable enable

using System.Globalization;
using System.Text.Json;

internal sealed class ActivityLogExporter
{
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly Func<string> _currentDirectory;
    private readonly Func<string, Stream> _createTempFile;

    public ActivityLogExporter()
        : this(
            () => DateTimeOffset.UtcNow,
            Directory.GetCurrentDirectory,
            CreateTempFile)
    {
    }

    internal ActivityLogExporter(
        Func<DateTimeOffset>? utcNow = null,
        Func<string>? currentDirectory = null,
        Func<string, Stream>? createTempFile = null)
    {
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        _currentDirectory = currentDirectory ?? Directory.GetCurrentDirectory;
        _createTempFile = createTempFile ?? CreateTempFile;
    }

    internal Task<string> ExportAsync(
        ActivityJournal journal,
        string? conversationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(journal);

        return Task.Run(
            () => Export(journal, conversationId, cancellationToken),
            cancellationToken);
    }

    internal static string CreatePrefix(string? conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId)
            || conversationId.Length < 10)
        {
            return "NO-ID";
        }

        ReadOnlySpan<char> source = conversationId.AsSpan(0, 10);
        if (source.Trim().IsEmpty)
        {
            return "NO-ID";
        }

        Span<char> prefix = stackalloc char[10];
        for (int index = 0; index < prefix.Length; index++)
        {
            char value = source[index];
            prefix[index] = IsPortableFilenameCharacter(value) ? value : '-';
        }

        string sanitized = new(prefix);
        return string.IsNullOrWhiteSpace(sanitized) ? "NO-ID" : sanitized;
    }

    private string Export(
        ActivityJournal journal,
        string? conversationId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<ActivityRecord> records = journal.Snapshot();
        string directory = _currentDirectory();
        string prefix = CreatePrefix(conversationId);
        string timestamp = _utcNow()
            .ToUniversalTime()
            .ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
        string baseFilename = $"{prefix}-{timestamp}";
        string tempPath = Path.Combine(
            directory,
            $".activity-log-{Guid.NewGuid():N}.tmp");

        try
        {
            WriteTemporaryFile(tempPath, records, cancellationToken);

            for (int collision = 0; ; collision++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string filename = collision == 0
                    ? $"{baseFilename}.json"
                    : $"{baseFilename}-{collision}.json";
                string finalPath = Path.Combine(directory, filename);

                try
                {
                    File.Move(tempPath, finalPath);
                    tempPath = string.Empty;
                    return filename;
                }
                catch (IOException) when (File.Exists(finalPath))
                {
                }
            }
        }
        finally
        {
            if (!string.IsNullOrEmpty(tempPath) && File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private void WriteTemporaryFile(
        string tempPath,
        IReadOnlyList<ActivityRecord> records,
        CancellationToken cancellationToken)
    {
        using Stream stream = _createTempFile(tempPath);
        using Utf8JsonWriter writer = new(
            stream,
            new JsonWriterOptions { Indented = true });
        writer.WriteStartArray();

        foreach (ActivityRecord record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (record.Direction == ActivityDirection.Diagnostic)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(record.Json))
            {
                throw new JsonException("Stored activity JSON is unavailable.");
            }

            using JsonDocument document = JsonDocument.Parse(record.Json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("Stored activity JSON must contain an object.");
            }

            document.RootElement.WriteTo(writer);
        }

        writer.WriteEndArray();
        writer.Flush();
        stream.Flush();
    }

    private static bool IsPortableFilenameCharacter(char value)
    {
        return value is >= 'a' and <= 'z'
            or >= 'A' and <= 'Z'
            or >= '0' and <= '9'
            or '-'
            or '_';
    }

    private static Stream CreateTempFile(string path)
    {
        return new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
    }
}
