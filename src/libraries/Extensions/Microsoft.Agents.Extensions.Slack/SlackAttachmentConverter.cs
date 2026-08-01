// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable enable

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Slack.Api;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Slack;

internal sealed class SlackAttachmentConverter
{
    private readonly ISlackFileUploader _fileUploader;
    private readonly ILogger<SlackAttachmentConverter> _logger;

    internal SlackAttachmentConverter(
        ISlackFileUploader fileUploader,
        ILogger<SlackAttachmentConverter>? logger = null)
    {
        _fileUploader = fileUploader ?? throw new ArgumentNullException(nameof(fileUploader));
        _logger = logger ?? NullLogger<SlackAttachmentConverter>.Instance;
    }

    internal async Task<IReadOnlyList<SlackPostAttachment>> ConvertAsync(
        IList<Attachment>? attachments,
        string callbackId,
        string channel,
        string token,
        bool renderButtonsAsMenu,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (attachments == null)
        {
            return [];
        }

        List<SlackPostAttachment> result = [];
        for (var index = 0; index < attachments.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var attachment = attachments[index];
            if (attachment == null)
            {
                _logger.LogWarning("Slack attachment {AttachmentIndex} is null.", index);
                continue;
            }

            try
            {
                var converted = await ConvertOneAsync(
                    attachment,
                    channel,
                    token,
                    renderButtonsAsMenu,
                    index,
                    cancellationToken).ConfigureAwait(false);

                result.AddRange(converted.Select(item => item with
                {
                    CallbackId = callbackId,
                }));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Unable to convert Slack attachment {AttachmentIndex} with content type {ContentType}.",
                    index,
                    attachment.ContentType);
            }
        }

        return result;
    }

    private async Task<IReadOnlyList<SlackPostAttachment>> ConvertOneAsync(
        Attachment attachment,
        string channel,
        string token,
        bool renderButtonsAsMenu,
        int attachmentIndex,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.Equals(attachment.ContentType, ContentTypes.AdaptiveCard, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Adaptive Card attachment {AttachmentIndex} is not supported by the direct Slack adapter.",
                attachmentIndex);
            return [];
        }

        if (string.Equals(attachment.ContentType, AudioCard.ContentType, StringComparison.Ordinal))
        {
            return ConvertMediaCard(ContentAs<AudioCard>(attachment), renderButtonsAsMenu);
        }

        if (string.Equals(attachment.ContentType, AnimationCard.ContentType, StringComparison.Ordinal))
        {
            return ConvertMediaCard(ContentAs<AnimationCard>(attachment), renderButtonsAsMenu);
        }

        if (string.Equals(attachment.ContentType, HeroCard.ContentType, StringComparison.Ordinal))
        {
            return ConvertBasicCard(ContentAs<HeroCard>(attachment), isHero: true, renderButtonsAsMenu);
        }

        if (string.Equals(attachment.ContentType, ThumbnailCard.ContentType, StringComparison.Ordinal))
        {
            return ConvertBasicCard(ContentAs<ThumbnailCard>(attachment), isHero: false, renderButtonsAsMenu);
        }

        if (string.Equals(attachment.ContentType, ReceiptCard.ContentType, StringComparison.Ordinal))
        {
            return ConvertReceiptCard(ContentAs<ReceiptCard>(attachment), renderButtonsAsMenu);
        }

        if (string.Equals(attachment.ContentType, SigninCard.ContentType, StringComparison.Ordinal))
        {
            return ConvertSigninCard(ContentAs<SigninCard>(attachment));
        }

        if (string.Equals(attachment.ContentType, OAuthCard.ContentType, StringComparison.Ordinal))
        {
            return ConvertOAuthCard(ContentAs<OAuthCard>(attachment));
        }

        if (string.Equals(attachment.ContentType, VideoCard.ContentType, StringComparison.Ordinal))
        {
            return ConvertMediaCard(ContentAs<VideoCard>(attachment), renderButtonsAsMenu);
        }

        return await ConvertGenericAttachmentAsync(
            attachment,
            channel,
            token,
            attachmentIndex,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<SlackPostAttachment>> ConvertGenericAttachmentAsync(
        Attachment attachment,
        string channel,
        string token,
        int attachmentIndex,
        CancellationToken cancellationToken)
    {
        var fileName = GetFileName(attachment, attachmentIndex);
        string? contentUrl = null;

        if (attachment.Content is byte[] content)
        {
            contentUrl = await _fileUploader.UploadAsync(
                content,
                fileName,
                channel,
                token,
                cancellationToken).ConfigureAwait(false);
        }
        else if (IsDataUrl(attachment.ContentUrl))
        {
            contentUrl = await _fileUploader.UploadAsync(
                DecodeDataUrl(attachment.ContentUrl),
                fileName,
                channel,
                token,
                cancellationToken).ConfigureAwait(false);
        }
        else if (IsHttpUrl(attachment.ContentUrl))
        {
            contentUrl = attachment.ContentUrl;
        }

        var thumbnailUrl = IsHttpUrl(attachment.ThumbnailUrl)
            ? attachment.ThumbnailUrl
            : null;
        var link = contentUrl ?? thumbnailUrl;
        if (link == null)
        {
            return [];
        }

        return
        [
            new SlackPostAttachment
            {
                Title = fileName,
                ImageUrl = contentUrl,
                TitleLink = link,
                Fallback = link,
                ThumbUrl = thumbnailUrl,
            },
        ];
    }

    private static string GetFileName(Attachment attachment, int attachmentIndex)
    {
        if (!string.IsNullOrWhiteSpace(attachment.Name))
        {
            return attachment.Name;
        }

        var name = attachmentIndex == 0
            ? "attachment"
            : $"attachment_{attachmentIndex + 1}";
        return name + GetMimeExtension(attachment.ContentType);
    }

    private static string GetMimeExtension(string? contentType)
    {
        if (string.Equals(contentType, "text/plain", StringComparison.OrdinalIgnoreCase))
        {
            return ".txt";
        }

        if (string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            return ".pdf";
        }

        if (string.Equals(contentType, "image/png", StringComparison.OrdinalIgnoreCase))
        {
            return ".png";
        }

        if (string.Equals(contentType, "image/jpeg", StringComparison.OrdinalIgnoreCase))
        {
            return ".jpg";
        }

        return string.Equals(contentType, "image/gif", StringComparison.OrdinalIgnoreCase)
            ? ".gif"
            : string.Empty;
    }

    private static bool IsDataUrl(string? url)
        => url?.StartsWith("data:", StringComparison.OrdinalIgnoreCase) == true;

    private static byte[] DecodeDataUrl(string dataUrl)
    {
        var commaIndex = dataUrl.IndexOf(',');
        if (commaIndex < 0
            || !dataUrl.Substring(5, commaIndex - 5)
                .EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException("The attachment data URL is not base64 encoded.");
        }

        try
        {
            return Convert.FromBase64String(
                Uri.UnescapeDataString(dataUrl.Substring(commaIndex + 1)));
        }
        catch (FormatException exception)
        {
            throw new FormatException("The attachment data URL contains invalid base64 content.", exception);
        }
    }

    private static bool IsHttpUrl(string? url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));

    private static List<SlackPostAttachment> ConvertBasicCard(
        HeroCard? card,
        bool isHero,
        bool renderButtonsAsMenu)
        => card == null
            ? []
            : ConvertBasicCard(
                card.Title,
                card.Subtitle,
                card.Text,
                card.Images,
                card.Buttons,
                card.Tap,
                isHero,
                renderButtonsAsMenu);

    private static List<SlackPostAttachment> ConvertBasicCard(
        ThumbnailCard? card,
        bool isHero,
        bool renderButtonsAsMenu)
        => card == null
            ? []
            : ConvertBasicCard(
                card.Title,
                card.Subtitle,
                card.Text,
                card.Images,
                card.Buttons,
                card.Tap,
                isHero,
                renderButtonsAsMenu);

    private static List<SlackPostAttachment> ConvertBasicCard(
        string? title,
        string? subtitle,
        string? text,
        IList<CardImage>? images,
        IList<CardAction>? buttons,
        CardAction? tap,
        bool isHero,
        bool renderButtonsAsMenu)
    {
        List<SlackPostAttachment> attachments = [];

        if (tap != null)
        {
            attachments.Add(RenderActionAttachment(tap));
        }

        var image = images?.FirstOrDefault();
        if (isHero && image != null)
        {
            attachments.Add(RenderImage(image));
        }

        var contentIndex = attachments.Count;
        attachments.Add(new SlackPostAttachment
        {
            Pretext = title,
            Title = subtitle,
            Text = text,
            ThumbUrl = isHero ? null : image?.Url ?? image?.Tap?.Image,
        });

        AddButtons(buttons, attachments, contentIndex, renderButtonsAsMenu);
        return attachments;
    }

    private static List<SlackPostAttachment> ConvertMediaCard(
        AudioCard? card,
        bool renderButtonsAsMenu)
        => card == null
            ? []
            : ConvertMediaCard(
                card.Title,
                card.Subtitle,
                card.Text,
                card.Image?.Url,
                card.Media,
                card.Buttons,
                renderButtonsAsMenu);

    private static List<SlackPostAttachment> ConvertMediaCard(
        AnimationCard? card,
        bool renderButtonsAsMenu)
        => card == null
            ? []
            : ConvertMediaCard(
                card.Title,
                card.Subtitle,
                card.Text,
                card.Image?.Url,
                card.Media,
                card.Buttons,
                renderButtonsAsMenu);

    private static List<SlackPostAttachment> ConvertMediaCard(
        VideoCard? card,
        bool renderButtonsAsMenu)
        => card == null
            ? []
            : ConvertMediaCard(
                card.Title,
                card.Subtitle,
                card.Text,
                card.Image?.Url,
                card.Media,
                card.Buttons,
                renderButtonsAsMenu);

    private static List<SlackPostAttachment> ConvertMediaCard(
        string? title,
        string? subtitle,
        string? text,
        string? imageUrl,
        IList<MediaUrl>? media,
        IList<CardAction>? buttons,
        bool renderButtonsAsMenu)
    {
        var firstMedia = media?.FirstOrDefault();
        var mediaUrl = firstMedia?.Url;
        var isGif = string.Equals(firstMedia?.Profile, "animation", StringComparison.OrdinalIgnoreCase)
            || HasGifExtension(mediaUrl);

        List<SlackPostAttachment> attachments =
        [
            new SlackPostAttachment
            {
                Pretext = title,
                Title = subtitle,
                Text = text,
                ImageUrl = isGif ? mediaUrl : imageUrl,
                TitleLink = isGif ? null : mediaUrl,
            },
        ];

        AddButtons(buttons, attachments, contentIndex: 0, renderButtonsAsMenu);
        return attachments;
    }

    private static List<SlackPostAttachment> ConvertReceiptCard(
        ReceiptCard? card,
        bool renderButtonsAsMenu)
    {
        if (card == null)
        {
            return [];
        }

        List<SlackPostField> fields = [];
        if (card.Items != null)
        {
            fields.AddRange(card.Items.Select(item => new SlackPostField(item.Title, item.Price)));
        }

        if (card.Facts != null)
        {
            fields.AddRange(card.Facts.Select(fact => new SlackPostField(fact.Key, fact.Value)));
        }

        if (!string.IsNullOrEmpty(card.Tax))
        {
            fields.Add(new SlackPostField("Tax", card.Tax));
        }

        if (!string.IsNullOrEmpty(card.Vat))
        {
            fields.Add(new SlackPostField("Vat", card.Vat));
        }

        if (!string.IsNullOrEmpty(card.Total))
        {
            fields.Add(new SlackPostField("Total", card.Total));
        }

        List<SlackPostAttachment> attachments =
        [
            new SlackPostAttachment
            {
                Pretext = card.Title,
                Fields = fields,
            },
        ];

        AddButtons(card.Buttons, attachments, contentIndex: 0, renderButtonsAsMenu);
        return attachments;
    }

    private static IReadOnlyList<SlackPostAttachment> ConvertSigninCard(SigninCard? card)
    {
        if (card == null)
        {
            return [];
        }

        return [RenderSigninAttachment(card.Text, card.Buttons.First())];
    }

    private static IReadOnlyList<SlackPostAttachment> ConvertOAuthCard(OAuthCard? card)
    {
        if (card == null)
        {
            return [];
        }

        return [RenderSigninAttachment(card.Text, card.Buttons.First())];
    }

    private static SlackPostAttachment RenderSigninAttachment(
        string? text,
        CardAction button)
    {
        var attachment = new SlackPostAttachment
        {
            Title = text,
            ThumbUrl = button.Image,
            Text = string.Empty,
        };

        var interactiveAction = ToInteractiveAction(button);
        return interactiveAction != null
            ? attachment with
            {
                Actions = [interactiveAction],
            }
            : attachment with
            {
                Fields = [ToLinkField(button)],
                MarkdownIn = ["fields"],
            };
    }

    private static SlackPostAttachment RenderImage(CardImage image)
    {
        string? title = null;
        if (image.Tap != null)
        {
            title = $"<{image.Tap.Value}|{image.Tap.Title}>";
        }

        return new SlackPostAttachment
        {
            Text = string.Empty,
            Title = title,
            ImageUrl = image.Url ?? image.Tap?.Image,
        };
    }

    private static SlackPostAttachment RenderActionAttachment(CardAction action)
    {
        var interactiveAction = ToInteractiveAction(action);
        return interactiveAction != null
            ? new SlackPostAttachment
            {
                Text = string.Empty,
                Actions = [interactiveAction],
            }
            : new SlackPostAttachment
            {
                Text = string.Empty,
                Fields = [ToLinkField(action)],
                MarkdownIn = ["fields"],
            };
    }

    private static void AddButtons(
        IList<CardAction>? buttons,
        List<SlackPostAttachment> attachments,
        int contentIndex,
        bool renderButtonsAsMenu)
    {
        if (buttons == null || buttons.Count == 0)
        {
            return;
        }

        List<SlackPostAction> interactiveActions = [];
        List<SlackPostField> fields = [];
        foreach (var button in buttons)
        {
            if (button == null)
            {
                continue;
            }

            var interactiveAction = ToInteractiveAction(button);
            if (interactiveAction != null)
            {
                interactiveActions.Add(interactiveAction);
            }
            else
            {
                fields.Add(ToLinkField(button));
            }
        }

        if (fields.Count > 0)
        {
            var content = attachments[contentIndex];
            attachments[contentIndex] = content with
            {
                Fields = (content.Fields ?? []).Concat(fields).ToList(),
                MarkdownIn = AddFieldsMarkdown(content.MarkdownIn),
            };
        }

        ProcessActions(interactiveActions, attachments, renderButtonsAsMenu);
    }

    private static void ProcessActions(
        List<SlackPostAction> actions,
        List<SlackPostAttachment> attachments,
        bool renderButtonsAsMenu)
    {
        if (actions.Count == 0)
        {
            return;
        }

        if (renderButtonsAsMenu)
        {
            var options = actions
                .Select(action => new SlackPostOption(action.Text, action.Value))
                .ToList();
            attachments.Add(new SlackPostAttachment
            {
                Text = string.Empty,
                AttachmentType = "default",
                Actions =
                [
                    new SlackPostAction(
                        Name: "select",
                        Text: string.Empty,
                        Type: "select",
                        Value: null,
                        Options: options,
                        SelectedOptions: [options[0]]),
                ],
            });
            return;
        }

        foreach (var chunk in actions.Chunk(5))
        {
            attachments.Add(new SlackPostAttachment
            {
                Text = string.Empty,
                Actions = chunk,
            });
        }
    }

    private static SlackPostAction? ToInteractiveAction(CardAction action)
    {
        if (action.Type != ActionTypes.ImBack
            && action.Type != ActionTypes.PostBack
            && action.Type != ActionTypes.MessageBack)
        {
            return null;
        }

        var value = action.Type == ActionTypes.MessageBack
            ? action.Text
            : action.Value as string;

        return new SlackPostAction(
            Name: action.Type,
            Text: action.Title,
            Type: "button",
            Value: value,
            Style: "default");
    }

    private static SlackPostField ToLinkField(CardAction action)
        => new(null, $"<{action.Value}|{action.Title}>");

    private static IReadOnlyList<string> AddFieldsMarkdown(IReadOnlyList<string>? markdownIn)
        => markdownIn?.Contains("fields") == true
            ? markdownIn
            : (markdownIn ?? []).Concat(["fields"]).ToList();

    private static bool HasGifExtension(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return false;
        }

        var path = Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? uri.AbsolutePath
            : url;
        return string.Equals(Path.GetExtension(path), ".gif", StringComparison.OrdinalIgnoreCase);
    }

    private static T? ContentAs<T>(Attachment attachment)
        => ProtocolJsonSerializer.ToObject<T>(attachment.Content);
}
