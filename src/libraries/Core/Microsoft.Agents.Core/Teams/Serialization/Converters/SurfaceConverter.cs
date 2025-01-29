// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Core.Teams.Models;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Core.Teams.Serialization.Converters
{
    /// <summary>
    /// Converter which allows json to be expression to object or static object.
    /// </summary>
    internal class SurfaceConverter : JsonConverter<Surface>
    {
        public override Surface Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var properties = options.PropertyNameCaseInsensitive
                ? new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, PropertyInfo>();

            foreach (var property in typeof(Surface).GetProperties())
            {
                properties.Add(property.Name, property);
            }

            SurfaceType surfaceType = SurfaceType.Unknown;
            ContentType contentType = ContentType.Unknown;
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    break;
                }

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = reader.GetString();
                    if (string.Equals("type", propertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        surfaceType = JsonSerializer.Deserialize<SurfaceType>(ref reader, options);
                    }
                    else if (string.Equals("contentType", propertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        contentType = JsonSerializer.Deserialize<ContentType>(ref reader, options);
                    }
                }
            }

            Surface parsedSurface;

            switch (surfaceType)
            {
                case SurfaceType.MeetingStage:
                    parsedSurface = CreateMeetingStageSurfaceWithContentType(contentType);
                    break;
                case SurfaceType.MeetingTabIcon:
                    parsedSurface = new MeetingTabIconSurface();
                    break;
                default:
                    throw new ArgumentException($"Invalid surface type: {surfaceType}");
            }

            return parsedSurface;
        }

        public override void Write(Utf8JsonWriter writer, Surface value, JsonSerializerOptions options)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(value, value?.GetType(), options);
            JsonDocument.Parse(json).WriteTo(writer);
        }

        private static Surface CreateMeetingStageSurfaceWithContentType(ContentType? contentType)
        {
            switch (contentType)
            {
                case ContentType.Task:
                    return new MeetingStageSurface<TaskModuleContinueResponse>();
                default:
                    throw new ArgumentException($"Invalid content type: {contentType}");
            }
        }
    }
}
