using System.Text.Json;

internal static class ActivityJsonFormatter
{
    private static readonly JsonSerializerOptions IndentedOptions = new()
    {
        WriteIndented = true
    };

    public static string Format(string protocolJson)
    {
        ArgumentNullException.ThrowIfNull(protocolJson);

        using JsonDocument document = JsonDocument.Parse(protocolJson);
        return JsonSerializer.Serialize(document.RootElement, IndentedOptions);
    }
}
