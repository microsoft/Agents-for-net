using System.Text.Json;
using Microsoft.Agents.Core.Serialization;

internal static class AdaptiveCardLinkExtractor
{
    public static LinkExtractionResult Extract(object? content)
    {
        if (content is null)
        {
            return new LinkExtractionResult([], null);
        }

        try
        {
            JsonElement normalized = Normalize(content);
            List<ChatLink> links = [];
            Visit(normalized, links);
            return new LinkExtractionResult(links, null);
        }
        catch (JsonException exception)
        {
            return new LinkExtractionResult([], exception.Message);
        }
        catch (NotSupportedException exception)
        {
            return new LinkExtractionResult([], exception.Message);
        }
    }

    private static JsonElement Normalize(object content)
    {
        if (content is JsonElement element)
        {
            return element.Clone();
        }

        if (content is JsonDocument document)
        {
            return document.RootElement.Clone();
        }

        if (content is string json)
        {
            using JsonDocument parsedDocument = JsonDocument.Parse(json);
            return parsedDocument.RootElement.Clone();
        }

        JsonElement serialized = JsonSerializer.SerializeToElement(
            content,
            content.GetType(),
            ProtocolJsonSerializer.SerializationOptions);
        return serialized.Clone();
    }

    private static void Visit(JsonElement element, List<ChatLink> links)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                Visit(item, links);
            }

            return;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (TryGetPropertyIgnoreCase(element, "type", out JsonElement typeElement)
            && string.Equals(typeElement.GetString(), "Action.OpenUrl", StringComparison.OrdinalIgnoreCase)
            && TryGetPropertyIgnoreCase(element, "url", out JsonElement urlElement)
            && Uri.TryCreate(urlElement.GetString(), UriKind.Absolute, out Uri? url)
            && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps))
        {
            string title = TryGetPropertyIgnoreCase(element, "title", out JsonElement titleElement)
                ? titleElement.GetString() ?? url.AbsoluteUri
                : url.AbsoluteUri;

            if (string.IsNullOrWhiteSpace(title))
            {
                title = url.AbsoluteUri;
            }

            links.Add(new ChatLink(title, url));
        }

        foreach (JsonProperty property in element.EnumerateObject())
        {
            Visit(property.Value, links);
        }
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
