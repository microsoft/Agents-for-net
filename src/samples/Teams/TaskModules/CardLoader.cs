// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Extensions.Teams;
using System.Reflection;
using System.Text.Json;

namespace TaskModules;

internal static class CardLoader
{
    public static JsonElement LoadCardJson(string fileName, IReadOnlyDictionary<string, string>? tokens = null, IList<string>? skipTokens = null)
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
        {
            if (!skipTokens?.Contains(key) ?? true)
            {
                json = json.Replace($"{{{{{key}}}}}", value);
            }
        }

        using var tokenizedDoc = JsonDocument.Parse(json);
        return tokenizedDoc.RootElement.Clone();
    }

    /// <summary>
    /// Load card using all data from the request as tokens for replacement in the card JSON. 
    /// </summary>
    /// <param name="request"></param>
    /// <param name="fileName"></param>
    /// <param name="dataKey"></param>
    /// <returns></returns>
    public static JsonElement LoadCardJson(Microsoft.Teams.Api.TaskModules.Request request, string fileName, string? dataKey = null )
    {
        return LoadCardJson(fileName, request.GetDataAs<IReadOnlyDictionary<string, string>>(), [dataKey ?? "task"]);
    }
}