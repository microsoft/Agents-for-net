using System.Text.Json;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;

internal static class ActivityJsonFormatter
{
    private static readonly JsonSerializerOptions IndentedOptions = new()
    {
        WriteIndented = true
    };

    public static string Format(Activity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);

        string protocolJson = ProtocolJsonSerializer.ToJson(activity);
        using JsonDocument document = JsonDocument.Parse(protocolJson);
        return JsonSerializer.Serialize(document.RootElement, IndentedOptions);
    }
}
