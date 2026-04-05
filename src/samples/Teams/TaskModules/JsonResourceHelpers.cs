// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Reflection;
using System.Text.Json;

namespace TaskModules;

internal static class JsonResourceHelpers
{
    public static JsonElement LoadCardJson(string fileName, IReadOnlyDictionary<string, string>? tokens = null)
    {
        var resourceName = $"TaskModules.Resources.{fileName}";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");

        if (tokens == null || tokens.Count == 0)
        {
            using var doc = JsonDocument.Parse(stream);
            return doc.RootElement.Clone();
        }

        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        foreach (var (key, value) in tokens)
            json = json.Replace($"{{{{{key}}}}}", value);

        using var tokenizedDoc = JsonDocument.Parse(json);
        return tokenizedDoc.RootElement.Clone();
    }
}