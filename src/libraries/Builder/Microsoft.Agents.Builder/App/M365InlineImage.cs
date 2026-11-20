// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Microsoft.Agents.Builder.App;

internal static class M365InlineImage
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);
    private static readonly Regex ImagePath = new(
        @"/v3/attachments/([A-Za-z0-9_.-]+)/views/original$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeout);
    private static readonly Regex ImageTag = new(
        @"<img\b(?:[^<>""']|""[^""]*""|'[^']*')*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeout);
    private static readonly Regex Attribute = new(
        @"(?<![A-Za-z0-9_-])([A-Za-z0-9_-]+)\s*=\s*(?:""([^""]*)""|'([^']*)')",
        RegexOptions.CultureInvariant, MatchTimeout);

    internal static string? GetAttachmentId(Attachment attachment, out Uri? uri)
    {
        uri = null;
        if (attachment.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) != true
            || !Uri.TryCreate(attachment.ContentUrl, UriKind.Absolute, out uri))
        {
            return null;
        }

        var match = ImagePath.Match(uri.AbsolutePath);
        return match.Success ? match.Groups[1].Value : null;
    }

    internal static bool IsUnderServiceUrl(Uri uri, string serviceUrl)
    {
        if (!Uri.TryCreate(serviceUrl, UriKind.Absolute, out var serviceUri)
            || uri.Scheme != Uri.UriSchemeHttps || serviceUri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(serviceUri.UserInfo)
            || Uri.Compare(uri, serviceUri, UriComponents.SchemeAndServer, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) != 0)
        {
            return false;
        }

        string prefix = NormalizeEscapes(serviceUri.AbsolutePath).TrimEnd('/') + "/";
        string path = NormalizeEscapes(uri.AbsolutePath);
        if (!path.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        string relativePath = path.Substring(prefix.Length);
        // Encoded separators or another decoding pass must not move the request outside the service path.
        return relativePath.IndexOf("%2F", StringComparison.Ordinal) < 0
            && relativePath.IndexOf("%5C", StringComparison.Ordinal) < 0
            && relativePath.IndexOf("%25", StringComparison.Ordinal) < 0;
    }

    internal static Dictionary<string, string> ReadHints(IList<Attachment> attachments, ILogger logger)
    {
        Dictionary<string, string> hints = new(StringComparer.Ordinal);
        foreach (var attachment in attachments)
        {
            if (attachment?.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) != true)
            {
                continue;
            }

            string? html = attachment.Content switch
            {
                string text => text,
                JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
                _ => null
            };
            if (html == null)
            {
                continue;
            }

            try
            {
                foreach (Match tag in ImageTag.Matches(html))
                {
                    Dictionary<string, string> attributes = new(StringComparer.OrdinalIgnoreCase);
                    foreach (Match attribute in Attribute.Matches(tag.Value))
                    {
                        attributes[attribute.Groups[1].Value] = attribute.Groups[2].Success
                            ? attribute.Groups[2].Value : attribute.Groups[3].Value;
                    }

                    if (attributes.TryGetValue("itemtype", out var itemType) && itemType == "http://schema.skype.com/AMSImage"
                        && attributes.TryGetValue("itemid", out var id) && !string.IsNullOrEmpty(id)
                        && attributes.TryGetValue("itemscope", out var format))
                    {
                        hints[id] = format;
                    }
                }
            }
            catch (RegexMatchTimeoutException)
            {
                logger.LogDebug("Inline image HTML hints were skipped because parsing exceeded the timeout.");
            }
        }

        return hints;
    }

    internal static void SetDownloadedMetadata(InputFile file, Attachment attachment, string id, Dictionary<string, string> hints)
    {
        string? hint = hints.TryGetValue(id, out var suppliedHint)
            ? suppliedHint
            : attachment.ContentType?.Substring(6).Split(';')[0];
        string? format = SniffFormat(file.Content.ToMemory().Span) ?? NormalizeFormat(hint);
        file.ContentType = format switch
        {
            "png" => "image/png",
            "jpg" => "image/jpeg",
            "gif" => "image/gif",
            "webp" => "image/webp",
            _ => "application/octet-stream"
        };
        file.Filename = format == null ? $"image-{id}" : $"image-{id}.{format}";
    }

    private static string? NormalizeFormat(string? format) => format?.Trim().ToLowerInvariant() switch
    {
        "png" => "png",
        "jpeg" or "jpg" => "jpg",
        "gif" => "gif",
        "webp" => "webp",
        _ => null
    };

    private static string? SniffFormat(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }))
        {
            return "png";
        }
        if (bytes.StartsWith(new byte[] { 0xff, 0xd8, 0xff }))
        {
            return "jpg";
        }
        if (bytes.StartsWith(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x37, 0x61 })
            || bytes.StartsWith(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }))
        {
            return "gif";
        }
        if (bytes.Length >= 12 && bytes.StartsWith(new byte[] { 0x52, 0x49, 0x46, 0x46 })
            && bytes.Slice(8).StartsWith(new byte[] { 0x57, 0x45, 0x42, 0x50 }))
        {
            return "webp";
        }
        return null;
    }

    private static string NormalizeEscapes(string path)
    {
        if (path.IndexOf('%') < 0)
        {
            return path;
        }

        var chars = path.ToCharArray();
        for (int i = 0; i < chars.Length - 2; i++)
        {
            if (chars[i] == '%' && Uri.IsHexDigit(chars[i + 1]) && Uri.IsHexDigit(chars[i + 2]))
            {
                chars[i + 1] = char.ToUpperInvariant(chars[i + 1]);
                chars[i + 2] = char.ToUpperInvariant(chars[i + 2]);
                i += 2;
            }
        }
        return new string(chars);
    }
}
