// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.Agents.Extensions.Slack.Api;

internal static class SlackStreamingContent
{
    internal static List<object> AttachmentBlocks(Attachment attachment)
    {
        AssertionHelpers.ThrowIfNull(attachment, nameof(attachment));

        var blocks = new List<object>();
        if (attachment.ContentType == ContentTypes.HeroCard)
        {
            var card = ProtocolJsonSerializer.ToObject<HeroCard>(attachment.Content);
            AddCard(blocks, card.Title, card.Subtitle, card.Text, card.Images, card.Buttons, card.Tap, false);
        }
        else if (attachment.ContentType == ContentTypes.ThumbnailCard)
        {
            var card = ProtocolJsonSerializer.ToObject<ThumbnailCard>(attachment.Content);
            AddCard(blocks, card.Title, card.Subtitle, card.Text, card.Images, card.Buttons, card.Tap, true);
        }
        else if (attachment.ContentType is "image/png" or "image/jpeg" or "image/jpg" or "image/gif")
        {
            blocks.Add(Image(attachment.ContentUrl, attachment.Name));
        }
        else if (!string.IsNullOrEmpty(attachment.ContentUrl)
            && attachment.ContentType?.StartsWith("application/vnd.microsoft.card.", StringComparison.OrdinalIgnoreCase) != true)
        {
            var url = HttpUrl(attachment.ContentUrl);
            blocks.Add(new { type = "markdown", text = $"[{Escape(attachment.Name ?? "Download file")}]({url})" });
        }
        else
        {
            throw new NotSupportedException($"Slack streaming does not support attachment type '{attachment.ContentType}'.");
        }
        return blocks;
    }

    internal static IEnumerable<object> CitationBlocks(string message, List<ClientCitation> citations)
    {
        var used = string.IsNullOrWhiteSpace(message)
            ? citations.GroupBy(citation => citation.Position).Select(group => group.First()).ToList()
            : CitationUtils.GetUsedCitations(message, citations);
        if (used == null)
        {
            yield break;
        }
        var heading = "**Sources**\n";
        foreach (var citation in used)
        {
            var name = Escape(citation.Appearance?.Name ?? $"Source {citation.Position}");
            var url = citation.Appearance?.Url;
            var source = !string.IsNullOrWhiteSpace(url) && Uri.TryCreate(url, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
                ? $"[{name}]({HttpUrl(url)})" : name;
            var text = $"{heading}[{citation.Position}] {source}";
            if (text.Length > 12000)
            {
                throw new NotSupportedException("A Slack citation exceeds the markdown block limit.");
            }
            yield return new { type = "markdown", text };
            heading = "";
        }
    }

    internal static object FeedbackBlock() => new
    {
        type = "context_actions",
        elements = new[]
        {
            new
            {
                type = "feedback_buttons",
                action_id = "feedback",
                positive_button = new { text = new { type = "plain_text", text = "\U0001F44D" }, value = "positive_feedback" },
                negative_button = new { text = new { type = "plain_text", text = "\U0001F44E" }, value = "negative_feedback" }
            }
        }
    };

    private static void AddCard(List<object> blocks, string title, string subtitle, string text,
        IList<CardImage> images, IList<CardAction> buttons, CardAction tap, bool thumbnail)
    {
        if (tap != null)
        {
            throw new NotSupportedException("Slack streaming does not support card-wide tap actions.");
        }
        if (thumbnail && images?.Count > 1)
        {
            throw new NotSupportedException("A Slack thumbnail card supports one image.");
        }

        var content = string.Join("\n", new[] { title, subtitle, text }.Where(value => !string.IsNullOrEmpty(value)));
        var offset = 0;
        do
        {
            var length = Math.Min(3000, content.Length - offset);
            if (length > 0 && offset + length < content.Length && char.IsHighSurrogate(content[offset + length - 1]))
            {
                length--;
            }
            var part = length > 0 ? content.Substring(offset, length) : " ";
            object accessory = thumbnail && offset == 0 && images?.Count > 0
                ? new { type = "image", image_url = HttpUrl(images[0].Url), alt_text = Clip(images[0].Alt ?? title ?? "Image", 2000) }
                : null;
            blocks.Add(new { type = "section", text = new { type = "plain_text", text = part }, accessory });
            offset += length;
        } while (offset < content.Length);

        if (!thumbnail && images != null)
        {
            foreach (var image in images)
            {
                blocks.Add(Image(image.Url, image.Alt ?? title));
            }
        }
        if (buttons == null)
        {
            return;
        }
        for (var start = 0; start < buttons.Count; start += 25)
        {
            var elements = new List<object>();
            foreach (var button in buttons.Skip(start).Take(25))
            {
                if (!string.Equals(button.Type, ActionTypes.OpenUrl, StringComparison.OrdinalIgnoreCase))
                {
                    throw new NotSupportedException("Slack streaming cards support OpenUrl buttons only.");
                }
                elements.Add(new
                {
                    type = "button",
                    text = new { type = "plain_text", text = Clip(button.Title ?? "Open link", 75) },
                    action_id = $"open_url_{start + elements.Count}",
                    url = HttpUrl(button.Value?.ToString())
                });
            }
            blocks.Add(new { type = "actions", elements });
        }
    }

    private static object Image(string url, string name) => new
    {
        type = "image",
        image_url = HttpUrl(url),
        alt_text = Clip(string.IsNullOrWhiteSpace(name) ? "Image" : name, 2000)
    };

    private static string HttpUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Slack attachments and citation links require an absolute HTTP or HTTPS URL.", nameof(value));
        }
        if (uri.AbsoluteUri.Length > 3000)
        {
            throw new ArgumentException("The Slack URL exceeds 3000 characters.", nameof(value));
        }
        return uri.AbsoluteUri.Replace("(", "%28").Replace(")", "%29");
    }

    private static string Escape(string text)
    {
        var result = new StringBuilder();
        foreach (var character in text)
        {
            if (character is '\\' or '`' or '*' or '_' or '{' or '}' or '[' or ']' or '(' or ')'
                or '#' or '+' or '-' or '.' or '!' or '<' or '>')
            {
                result.Append('\\');
            }
            result.Append(character);
        }
        return result.ToString();
    }

    private static string Clip(string text, int length) => text.Length <= length ? text : text.Substring(0, length);
}
