// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Microsoft.Agents.Builder.Testing.Http
{
    internal static class HttpSimulationDiagnostics
    {
        private const string Redacted = "[REDACTED]";

        internal static Uri SanitizeUri(Uri uri)
        {
            if (uri == null || !uri.IsAbsoluteUri)
            {
                return null;
            }

            var builder = new UriBuilder(uri)
            {
                UserName = string.Empty,
                Password = string.Empty,
                Fragment = string.Empty,
                Query = string.Join("&", HttpRequestMatcher.ParseQuery(uri.Query).Select(pair =>
                    Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(IsCredential(pair.Key) ? Redacted : pair.Value)))
            };
            return builder.Uri;
        }

        internal static IReadOnlyDictionary<string, IReadOnlyList<string>> SnapshotHeaders(HttpRequestMessage request)
        {
            var headers = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            var source = request.Content == null ? request.Headers : request.Headers.Concat(request.Content.Headers);
            foreach (var header in source)
            {
                headers[header.Key] = Array.AsReadOnly(IsCredential(header.Key) ? [Redacted] : header.Value.ToArray());
            }

            return new ReadOnlyDictionary<string, IReadOnlyList<string>>(headers);
        }

        internal static string CaptureBody(byte[] bytes, HttpContent content)
        {
            var charset = content?.Headers.ContentType?.CharSet?.Trim('"');
            var encoding = string.IsNullOrEmpty(charset) ? Encoding.UTF8 : Encoding.GetEncoding(charset);
            var text = encoding.GetString(bytes);
            var mediaType = content?.Headers.ContentType?.MediaType;
            if (string.Equals(mediaType, "application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
            {
                return string.Join("&", HttpRequestMatcher.ParseQuery(text).Select(pair =>
                    Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(IsCredential(pair.Key) ? Redacted : pair.Value)));
            }

            try
            {
                using var document = JsonDocument.Parse(text);
                using var output = new System.IO.MemoryStream();
                using (var writer = new Utf8JsonWriter(output))
                {
                    WriteSanitizedJson(writer, document.RootElement);
                }

                return Encoding.UTF8.GetString(output.ToArray());
            }
            catch (JsonException)
            {
                return string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase)
                    || mediaType?.EndsWith("+json", StringComparison.OrdinalIgnoreCase) == true
                    ? Redacted
                    : text;
            }
        }

        private static void WriteSanitizedJson(Utf8JsonWriter writer, JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Object)
            {
                writer.WriteStartObject();
                foreach (var property in value.EnumerateObject())
                {
                    writer.WritePropertyName(property.Name);
                    if (IsCredential(property.Name))
                    {
                        writer.WriteStringValue(Redacted);
                    }
                    else
                    {
                        WriteSanitizedJson(writer, property.Value);
                    }
                }

                writer.WriteEndObject();
            }
            else if (value.ValueKind == JsonValueKind.Array)
            {
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray())
                {
                    WriteSanitizedJson(writer, item);
                }

                writer.WriteEndArray();
            }
            else
            {
                value.WriteTo(writer);
            }
        }

        private static bool IsCredential(string name)
        {
            var normalized = new string(name.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
            return normalized.Contains("authorization") || normalized.Contains("cookie")
                || normalized.Contains("token") || normalized.Contains("key")
                || normalized.Contains("secret") || normalized.Contains("password")
                || normalized.Contains("credential") || normalized.Contains("signature")
                || normalized.Contains("assertion") || normalized == "sig"
                || normalized == "devicecode" || normalized == "codeverifier"
                || normalized == "code" || normalized == "auth" || normalized == "sas";
        }
    }
}
