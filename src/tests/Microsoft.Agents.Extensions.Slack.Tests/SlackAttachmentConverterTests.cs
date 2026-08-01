// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable enable

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Slack.Api;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Extensions.Slack.Tests;

public class SlackAttachmentConverterTests
{
    private const string CallbackId = "B1:T1";
    private const string Channel = "C1";
    private const string Token = "xoxb-token";

    [Fact]
    public async Task ConvertAsync_HeroCard_MapsTapImageTextButtonsAndLinks()
    {
        var card = new HeroCard(
            title: "Title",
            subtitle: "Subtitle",
            text: "Body",
            images: [new CardImage("https://example.test/hero.png")],
            buttons:
            [
                new CardAction(ActionTypes.ImBack, "Reply", value: "reply-value"),
                new CardAction(ActionTypes.MessageBack, "Message", text: "message-text", value: "ignored"),
                new CardAction(ActionTypes.OpenUrl, "Open", value: "https://example.test"),
            ],
            tap: new CardAction(ActionTypes.PostBack, "Tap", value: "tap-value"));

        var result = await CreateConverter().ConvertAsync(
            [card.ToAttachment()],
            CallbackId,
            Channel,
            Token,
            renderButtonsAsMenu: false,
            CancellationToken.None);

        Assert.All(result, attachment => Assert.Equal(CallbackId, attachment.CallbackId));

        var tap = Assert.Single(
            result,
            attachment => attachment.Actions?.Any(action => action.Text == "Tap") == true);
        var tapAction = Assert.Single(tap.Actions!);
        Assert.Equal(ActionTypes.PostBack, tapAction.Name);
        Assert.Equal("button", tapAction.Type);
        Assert.Equal("default", tapAction.Style);
        Assert.Equal("tap-value", tapAction.Value);

        Assert.Contains(result, attachment =>
            attachment.ImageUrl == "https://example.test/hero.png");

        var content = Assert.Single(result, attachment => attachment.Pretext == "Title");
        Assert.Equal("Subtitle", content.Title);
        Assert.Equal("Body", content.Text);
        var link = Assert.Single(content.Fields!);
        Assert.Equal("<https://example.test|Open>", link.Value);
        Assert.Contains("fields", content.MarkdownIn!);

        var actions = Assert.Single(
            result,
            attachment => attachment.Actions?.Count == 2);
        Assert.Collection(
            actions.Actions!,
            action =>
            {
                Assert.Equal(ActionTypes.ImBack, action.Name);
                Assert.Equal("reply-value", action.Value);
            },
            action =>
            {
                Assert.Equal(ActionTypes.MessageBack, action.Name);
                Assert.Equal("message-text", action.Value);
            });
    }

    [Fact]
    public async Task ConvertAsync_ThumbnailCardFromJsonElement_MapsTapContentAndThumbnail()
    {
        var card = new ThumbnailCard(
            title: "Thumbnail title",
            subtitle: "Thumbnail subtitle",
            text: "Thumbnail body",
            images: [new CardImage("https://example.test/thumb.png")],
            tap: new CardAction(ActionTypes.OpenUrl, "Tap link", value: "https://example.test/tap"));
        var attachment = card.ToAttachment();
        attachment.Content = JsonSerializer.SerializeToElement(
            card,
            ProtocolJsonSerializer.SerializationOptions);

        var result = await CreateConverter().ConvertAsync(
            [attachment],
            CallbackId,
            Channel,
            Token,
            renderButtonsAsMenu: false,
            CancellationToken.None);

        Assert.All(result, item => Assert.Equal(CallbackId, item.CallbackId));

        var tap = Assert.Single(result, item => item.Pretext == null);
        Assert.Equal("<https://example.test/tap|Tap link>", Assert.Single(tap.Fields!).Value);
        Assert.Contains("fields", tap.MarkdownIn!);

        var content = Assert.Single(result, item => item.Pretext == "Thumbnail title");
        Assert.Equal("Thumbnail subtitle", content.Title);
        Assert.Equal("Thumbnail body", content.Text);
        Assert.Equal("https://example.test/thumb.png", content.ThumbUrl);
    }

    [Fact]
    public async Task ConvertAsync_AudioCard_MapsMediaLinkAndImage()
    {
        var card = new AudioCard(
            title: "Audio title",
            subtitle: "Audio subtitle",
            text: "Audio body",
            image: new ThumbnailUrl("https://example.test/audio.png"),
            media: [new MediaUrl("https://example.test/audio.mp3", "audio")]);

        var attachment = Assert.Single(await CreateConverter().ConvertAsync(
            [card.ToAttachment()],
            CallbackId,
            Channel,
            Token,
            renderButtonsAsMenu: false,
            CancellationToken.None));

        Assert.Equal("Audio title", attachment.Pretext);
        Assert.Equal("Audio subtitle", attachment.Title);
        Assert.Equal("Audio body", attachment.Text);
        Assert.Equal("https://example.test/audio.png", attachment.ImageUrl);
        Assert.Equal("https://example.test/audio.mp3", attachment.TitleLink);
        Assert.Equal(CallbackId, attachment.CallbackId);
    }

    [Fact]
    public async Task ConvertAsync_AnimationCardWithAnimationProfile_MapsMediaAsImage()
    {
        var card = new AnimationCard(
            title: "Animation title",
            media: [new MediaUrl("https://example.test/animation.bin", "animation")]);

        var attachment = Assert.Single(await CreateConverter().ConvertAsync(
            [card.ToAttachment()],
            CallbackId,
            Channel,
            Token,
            renderButtonsAsMenu: false,
            CancellationToken.None));

        Assert.Equal("https://example.test/animation.bin", attachment.ImageUrl);
        Assert.Null(attachment.TitleLink);
    }

    [Fact]
    public async Task ConvertAsync_VideoCardWithGifUrlFromJsonElement_MapsMediaAsImage()
    {
        var card = new VideoCard(
            title: "Video title",
            media: [new MediaUrl("https://example.test/video.gif?version=1", "video")]);
        var attachment = card.ToAttachment();
        attachment.Content = JsonSerializer.SerializeToElement(
            card,
            ProtocolJsonSerializer.SerializationOptions);

        var result = Assert.Single(await CreateConverter().ConvertAsync(
            [attachment],
            CallbackId,
            Channel,
            Token,
            renderButtonsAsMenu: false,
            CancellationToken.None));

        Assert.Equal("https://example.test/video.gif?version=1", result.ImageUrl);
        Assert.Null(result.TitleLink);
    }

    [Fact]
    public async Task ConvertAsync_ReceiptCard_MapsFieldsAndButtons()
    {
        var card = new ReceiptCard(
            title: "Receipt",
            facts: [new Fact("Order", "42")],
            items: [new ReceiptItem(title: "Coffee", price: "$5")],
            total: "$6.50",
            tax: "$1",
            vat: "$0.50",
            buttons:
            [
                new CardAction(ActionTypes.ImBack, "Repeat", value: "repeat"),
                new CardAction(ActionTypes.OpenUrl, "Details", value: "https://example.test/order"),
            ]);

        var result = await CreateConverter().ConvertAsync(
            [card.ToAttachment()],
            CallbackId,
            Channel,
            Token,
            renderButtonsAsMenu: false,
            CancellationToken.None);

        var receipt = Assert.Single(result, attachment => attachment.Pretext == "Receipt");
        Assert.Collection(
            receipt.Fields!,
            field =>
            {
                Assert.Equal("Coffee", field.Title);
                Assert.Equal("$5", field.Value);
            },
            field =>
            {
                Assert.Equal("Order", field.Title);
                Assert.Equal("42", field.Value);
            },
            field =>
            {
                Assert.Equal("Tax", field.Title);
                Assert.Equal("$1", field.Value);
            },
            field =>
            {
                Assert.Equal("Vat", field.Title);
                Assert.Equal("$0.50", field.Value);
            },
            field =>
            {
                Assert.Equal("Total", field.Title);
                Assert.Equal("$6.50", field.Value);
            },
            field =>
            {
                Assert.Null(field.Title);
                Assert.Equal("<https://example.test/order|Details>", field.Value);
            });
        Assert.Contains("fields", receipt.MarkdownIn!);

        var button = Assert.Single(result.SelectMany(attachment => attachment.Actions ?? []));
        Assert.Equal("repeat", button.Value);
        Assert.All(result, attachment => Assert.Equal(CallbackId, attachment.CallbackId));
    }

    [Fact]
    public async Task ConvertAsync_SigninAndOAuthCards_UseFirstButton()
    {
        var signin = new SigninCard(
            text: "Sign in",
            buttons:
            [
                new CardAction(ActionTypes.Signin, "Continue", image: "https://example.test/signin.png", value: "https://example.test/signin"),
                new CardAction(ActionTypes.Signin, "Ignored", value: "https://example.test/ignored"),
            ]);
        var oauth = new OAuthCard(
            text: "Authorize",
            connectionName: "connection",
            buttons:
            [
                new CardAction(ActionTypes.ImBack, "Authorize now", image: "https://example.test/oauth.png", value: "authorize"),
                new CardAction(ActionTypes.ImBack, "Ignored", value: "ignored"),
            ]);

        var result = await CreateConverter().ConvertAsync(
            [signin.ToAttachment(), oauth.ToAttachment()],
            CallbackId,
            Channel,
            Token,
            renderButtonsAsMenu: false,
            CancellationToken.None);

        Assert.Collection(
            result,
            attachment =>
            {
                Assert.Equal("Sign in", attachment.Title);
                Assert.Equal("https://example.test/signin.png", attachment.ThumbUrl);
                Assert.Equal(string.Empty, attachment.Text);
                Assert.Equal("<https://example.test/signin|Continue>", Assert.Single(attachment.Fields!).Value);
                Assert.DoesNotContain("Ignored", attachment.Fields!.Select(field => field.Value));
            },
            attachment =>
            {
                Assert.Equal("Authorize", attachment.Title);
                Assert.Equal("https://example.test/oauth.png", attachment.ThumbUrl);
                Assert.Equal(string.Empty, attachment.Text);
                var action = Assert.Single(attachment.Actions!);
                Assert.Equal("Authorize now", action.Text);
                Assert.Equal("authorize", action.Value);
            });
    }

    [Fact]
    public async Task ConvertAsync_SixInteractiveButtons_SplitsFiveAndOne()
    {
        var card = new HeroCard(
            title: "Buttons",
            buttons: Enumerable.Range(1, 6)
                .Select(index => new CardAction(ActionTypes.PostBack, $"Button {index}", value: $"value-{index}"))
                .ToList());

        var result = await CreateConverter().ConvertAsync(
            [card.ToAttachment()],
            CallbackId,
            Channel,
            Token,
            renderButtonsAsMenu: false,
            CancellationToken.None);

        var actionAttachments = result.Where(attachment => attachment.Actions != null).ToList();
        Assert.Collection(
            actionAttachments,
            attachment => Assert.Equal(5, attachment.Actions!.Count),
            attachment => Assert.Single(attachment.Actions!));
        Assert.All(actionAttachments, attachment => Assert.Equal(CallbackId, attachment.CallbackId));
    }

    [Fact]
    public async Task ConvertAsync_RenderButtonsAsMenu_CreatesSelectWithOptionsAndSelection()
    {
        var card = new HeroCard(
            title: "Menu",
            buttons:
            [
                new CardAction(ActionTypes.ImBack, "First", value: "first"),
                new CardAction(ActionTypes.MessageBack, "Second", text: "second-text", value: "ignored"),
                new CardAction(ActionTypes.OpenUrl, "Open", value: "https://example.test"),
            ]);

        var result = await CreateConverter().ConvertAsync(
            [card.ToAttachment()],
            CallbackId,
            Channel,
            Token,
            renderButtonsAsMenu: true,
            CancellationToken.None);

        var menuAttachment = Assert.Single(
            result,
            attachment => attachment.Actions?.SingleOrDefault()?.Type == "select");
        Assert.Equal("default", menuAttachment.AttachmentType);
        var select = Assert.Single(menuAttachment.Actions!);
        Assert.Equal("select", select.Name);
        Assert.Equal(string.Empty, select.Text);
        Assert.Collection(
            select.Options!,
            option =>
            {
                Assert.Equal("First", option.Text);
                Assert.Equal("first", option.Value);
            },
            option =>
            {
                Assert.Equal("Second", option.Text);
                Assert.Equal("second-text", option.Value);
            });
        var selected = Assert.Single(select.SelectedOptions!);
        Assert.Equal("First", selected.Text);
        Assert.Equal("first", selected.Value);

        var content = Assert.Single(result, attachment => attachment.Pretext == "Menu");
        Assert.Equal("<https://example.test|Open>", Assert.Single(content.Fields!).Value);
    }

    [Fact]
    public async Task ConvertAsync_AdaptiveCard_LogsWarningAndOmitsAttachment()
    {
        var logger = new RecordingLogger<SlackAttachmentConverter>();
        var converter = CreateConverter(logger);

        var result = await converter.ConvertAsync(
            [new Attachment(ContentTypes.AdaptiveCard, content: new { type = "AdaptiveCard" })],
            CallbackId,
            Channel,
            Token,
            renderButtonsAsMenu: false,
            CancellationToken.None);

        Assert.Empty(result);
        var warning = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Contains("Adaptive Card attachment 0", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConvertAsync_NullAttachment_LogsWarningAndContinues()
    {
        var logger = new RecordingLogger<SlackAttachmentConverter>();
        var converter = CreateConverter(logger);

        var result = await converter.ConvertAsync(
            [null!, new HeroCard(title: "Valid").ToAttachment()],
            CallbackId,
            Channel,
            Token,
            renderButtonsAsMenu: false,
            CancellationToken.None);

        Assert.Contains(result, attachment => attachment.Pretext == "Valid");
        Assert.Contains(logger.Entries, entry =>
            entry.Level == LogLevel.Warning
            && entry.Message.Contains("attachment 0 is null", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ConvertAsync_FailedAttachment_LogsIndexAndContentTypeAndContinues()
    {
        var logger = new RecordingLogger<SlackAttachmentConverter>();
        var converter = CreateConverter(logger);
        var malformed = new Attachment(
            HeroCard.ContentType,
            content: JsonSerializer.SerializeToElement(42));

        var result = await converter.ConvertAsync(
            [malformed, new ThumbnailCard(title: "Valid").ToAttachment()],
            CallbackId,
            Channel,
            Token,
            renderButtonsAsMenu: false,
            CancellationToken.None);

        Assert.Contains(result, attachment => attachment.Pretext == "Valid");
        Assert.Contains(logger.Entries, entry =>
            entry.Level == LogLevel.Warning
            && entry.Exception != null
            && entry.Message.Contains("attachment 0", StringComparison.Ordinal)
            && entry.Message.Contains(HeroCard.ContentType, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ConvertAsync_CanceledOperation_Propagates()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateConverter().ConvertAsync(
                [new HeroCard(title: "Canceled").ToAttachment()],
                CallbackId,
                Channel,
                Token,
                renderButtonsAsMenu: false,
                cancellation.Token));
    }

    [Fact]
    public async Task ConvertAsync_InlineByteArray_UploadsAndRendersGenericAttachment()
    {
        var uploader = new TestSlackFileUploader((_, _) =>
            Task.FromResult<string?>("https://files.slack.test/private"));
        var converter = new SlackAttachmentConverter(uploader);

        var result = Assert.Single(await converter.ConvertAsync(
            [
                new Attachment(
                    "application/pdf",
                    content: new byte[] { 1, 2, 3 },
                    name: "report.pdf",
                    thumbnailUrl: "https://example.test/thumb.png"),
            ],
            CallbackId,
            Channel,
            Token,
            renderButtonsAsMenu: false,
            CancellationToken.None));

        var upload = Assert.Single(uploader.Calls);
        Assert.Equal(new byte[] { 1, 2, 3 }, upload.Content);
        Assert.Equal("report.pdf", upload.FileName);
        Assert.Equal(Channel, upload.Channel);
        Assert.Equal(Token, upload.Token);
        Assert.Equal("report.pdf", result.Title);
        Assert.Equal("https://files.slack.test/private", result.ImageUrl);
        Assert.Equal("https://files.slack.test/private", result.TitleLink);
        Assert.Equal("https://files.slack.test/private", result.Fallback);
        Assert.Equal("https://example.test/thumb.png", result.ThumbUrl);
        Assert.Equal(CallbackId, result.CallbackId);
    }

    [Fact]
    public async Task ConvertAsync_Base64DataUrl_DecodesUploadsAndRendersAttachment()
    {
        var uploader = new TestSlackFileUploader((_, _) =>
            Task.FromResult<string?>("https://files.slack.test/note"));
        var converter = new SlackAttachmentConverter(uploader);

        var result = Assert.Single(await converter.ConvertAsync(
            [
                new Attachment(
                    "text/plain",
                    contentUrl: "data:text/plain;base64,aGVsbG8=",
                    name: "note.txt"),
            ],
            CallbackId,
            Channel,
            Token,
            renderButtonsAsMenu: false,
            CancellationToken.None));

        var upload = Assert.Single(uploader.Calls);
        Assert.Equal(Encoding.UTF8.GetBytes("hello"), upload.Content);
        Assert.Equal("note.txt", upload.FileName);
        Assert.Equal("https://files.slack.test/note", result.ImageUrl);
    }

    [Fact]
    public async Task ConvertAsync_HttpContentUrl_ReferencesUrlWithoutUploading()
    {
        var uploader = new TestSlackFileUploader();
        var converter = new SlackAttachmentConverter(uploader);

        var result = Assert.Single(await converter.ConvertAsync(
            [
                new Attachment(
                    "image/png",
                    contentUrl: "https://example.test/content.png",
                    name: "content.png",
                    thumbnailUrl: "https://example.test/thumb.png"),
            ],
            CallbackId,
            Channel,
            Token,
            renderButtonsAsMenu: false,
            CancellationToken.None));

        Assert.Empty(uploader.Calls);
        Assert.Equal("content.png", result.Title);
        Assert.Equal("https://example.test/content.png", result.ImageUrl);
        Assert.Equal("https://example.test/content.png", result.TitleLink);
        Assert.Equal("https://example.test/content.png", result.Fallback);
        Assert.Equal("https://example.test/thumb.png", result.ThumbUrl);
    }

    [Fact]
    public async Task ConvertAsync_ThumbnailOnly_ReferencesThumbnailWithoutUploading()
    {
        var uploader = new TestSlackFileUploader();
        var converter = new SlackAttachmentConverter(uploader);

        var result = Assert.Single(await converter.ConvertAsync(
            [
                new Attachment(
                    "image/jpeg",
                    name: "preview.jpg",
                    thumbnailUrl: "https://example.test/preview.jpg"),
            ],
            CallbackId,
            Channel,
            Token,
            renderButtonsAsMenu: false,
            CancellationToken.None));

        Assert.Empty(uploader.Calls);
        Assert.Equal("preview.jpg", result.Title);
        Assert.Null(result.ImageUrl);
        Assert.Equal("https://example.test/preview.jpg", result.TitleLink);
        Assert.Equal("https://example.test/preview.jpg", result.Fallback);
        Assert.Equal("https://example.test/preview.jpg", result.ThumbUrl);
    }

    [Fact]
    public async Task ConvertAsync_UnnamedAttachments_GenerateSequentialNamesWithKnownMimeExtensions()
    {
        var uploader = new TestSlackFileUploader((call, _) =>
            Task.FromResult<string?>($"https://files.slack.test/{call.FileName}"));
        var converter = new SlackAttachmentConverter(uploader);

        var result = await converter.ConvertAsync(
            [
                new Attachment("text/plain", content: new byte[] { 1 }),
                new Attachment("application/pdf", content: new byte[] { 2 }),
                new Attachment("image/png", content: new byte[] { 3 }),
                new Attachment("image/jpeg", content: new byte[] { 4 }),
                new Attachment("image/gif", content: new byte[] { 5 }),
                new Attachment("application/octet-stream", content: new byte[] { 6 }),
            ],
            CallbackId,
            Channel,
            Token,
            renderButtonsAsMenu: false,
            CancellationToken.None);

        Assert.Equal(
            [
                "attachment.txt",
                "attachment_2.pdf",
                "attachment_3.png",
                "attachment_4.jpg",
                "attachment_5.gif",
                "attachment_6",
            ],
            uploader.Calls.Select(call => call.FileName));
        Assert.Equal(
            uploader.Calls.Select(call => call.FileName),
            result.Select(attachment => attachment.Title));
    }

    [Fact]
    public async Task ConvertAsync_UploadFailure_LogsAndContinuesWithLaterAttachment()
    {
        var logger = new RecordingLogger<SlackAttachmentConverter>();
        var uploader = new TestSlackFileUploader((call, _) =>
            call.FileName == "first.txt"
                ? throw new SlackResponseException("upload failed")
                : Task.FromResult<string?>("https://files.slack.test/second"));
        var converter = new SlackAttachmentConverter(uploader, logger);

        var result = await converter.ConvertAsync(
            [
                new Attachment("text/plain", content: new byte[] { 1 }, name: "first.txt"),
                new Attachment("text/plain", content: new byte[] { 2 }, name: "second.txt"),
            ],
            CallbackId,
            Channel,
            Token,
            renderButtonsAsMenu: false,
            CancellationToken.None);

        var rendered = Assert.Single(result);
        Assert.Equal("second.txt", rendered.Title);
        Assert.Equal(2, uploader.Calls.Count);
        Assert.Contains(logger.Entries, entry =>
            entry.Level == LogLevel.Warning
            && entry.Exception is SlackResponseException
            && entry.Message.Contains("attachment 0", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ConvertAsync_AttachmentFailureLoggerThrows_ContinuesWithLaterAttachment()
    {
        var logger = new ThrowingLogger<SlackAttachmentConverter>(
            new InvalidOperationException("Logger failed"));
        var uploader = new TestSlackFileUploader((call, _) =>
            call.FileName == "first.txt"
                ? throw new SlackResponseException("upload failed")
                : Task.FromResult<string?>("https://files.slack.test/second"));
        var converter = new SlackAttachmentConverter(uploader, logger);

        var result = await converter.ConvertAsync(
            [
                new Attachment("text/plain", content: new byte[] { 1 }, name: "first.txt"),
                new Attachment("text/plain", content: new byte[] { 2 }, name: "second.txt"),
            ],
            CallbackId,
            Channel,
            Token,
            renderButtonsAsMenu: false,
            CancellationToken.None);

        var rendered = Assert.Single(result);
        Assert.Equal("second.txt", rendered.Title);
        Assert.Equal(2, uploader.Calls.Count);
    }

    [Fact]
    public async Task ConvertAsync_AttachmentFailureLoggerCancellation_Propagates()
    {
        var logger = new ThrowingLogger<SlackAttachmentConverter>(
            new OperationCanceledException("Logger canceled"));
        var uploader = new TestSlackFileUploader((_, _) =>
            throw new SlackResponseException("upload failed"));
        var converter = new SlackAttachmentConverter(uploader, logger);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            converter.ConvertAsync(
                [new Attachment("text/plain", content: new byte[] { 1 })],
                CallbackId,
                Channel,
                Token,
                renderButtonsAsMenu: false,
                CancellationToken.None));
    }

    [Fact]
    public async Task ConvertAsync_InvalidDataUrl_LogsAndContinuesWithLaterAttachment()
    {
        var logger = new RecordingLogger<SlackAttachmentConverter>();
        var uploader = new TestSlackFileUploader();
        var converter = new SlackAttachmentConverter(uploader, logger);

        var result = await converter.ConvertAsync(
            [
                new Attachment(
                    "text/plain",
                    contentUrl: "data:text/plain,not-base64",
                    name: "invalid.txt"),
                new Attachment(
                    "text/plain",
                    contentUrl: "https://example.test/valid.txt",
                    name: "valid.txt"),
            ],
            CallbackId,
            Channel,
            Token,
            renderButtonsAsMenu: false,
            CancellationToken.None);

        var rendered = Assert.Single(result);
        Assert.Equal("valid.txt", rendered.Title);
        Assert.Empty(uploader.Calls);
        Assert.Contains(logger.Entries, entry =>
            entry.Level == LogLevel.Warning
            && entry.Exception is FormatException
            && entry.Message.Contains("attachment 0", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ConvertAsync_CancellationFromUpload_Propagates()
    {
        using var cancellation = new CancellationTokenSource();
        var uploader = new TestSlackFileUploader((_, cancellationToken) =>
        {
            cancellation.Cancel();
            return Task.FromCanceled<string?>(cancellationToken);
        });
        var converter = new SlackAttachmentConverter(uploader);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            converter.ConvertAsync(
                [new Attachment("text/plain", content: new byte[] { 1 })],
                CallbackId,
                Channel,
                Token,
                renderButtonsAsMenu: false,
                cancellation.Token));
    }

    [Fact]
    public async Task SlackMessageConverter_AttachmentOnlyActivity_ReturnsPayloadWithConvertedAttachments()
    {
        var converter = new SlackMessageConverter(CreateConverter());
        var activity = new Activity
        {
            Type = ActivityTypes.Message,
            From = new ChannelAccount(CallbackId),
            Attachments =
            [
                new HeroCard(
                    title: "Attachment only",
                    buttons: [new CardAction(ActionTypes.ImBack, "Choose", value: "choice")])
                    .ToAttachment(),
            ],
            ChannelData = new SlackChannelData
            {
                RenderButtonsAsMenu = true,
            },
        };

        var payload = Assert.Single(await converter.ConvertAsync(
            activity,
            Channel,
            "123.456",
            Token,
            CancellationToken.None));

        Assert.Equal(Channel, payload.Channel);
        Assert.Equal("123.456", payload.ThreadTs);
        Assert.Null(payload.Text);
        Assert.NotEmpty(payload.Attachments!);
        Assert.All(payload.Attachments!, attachment => Assert.Equal(CallbackId, attachment.CallbackId));
        Assert.Contains(payload.Attachments!, attachment =>
            attachment.Actions?.SingleOrDefault()?.Type == "select");
    }

    [Fact]
    public void SlackChannelData_RenderButtonsAsMenu_UsesSnakeCaseJsonProperty()
    {
        var json = JsonSerializer.Serialize(new SlackChannelData
        {
            RenderButtonsAsMenu = true,
        });
        var roundTrip = JsonSerializer.Deserialize<SlackChannelData>(
            """{"render_buttons_as_menu":true}""");

        Assert.Contains(
            "\"render_buttons_as_menu\":true",
            json,
            StringComparison.Ordinal);
        Assert.True(roundTrip!.RenderButtonsAsMenu);
    }

    private static SlackAttachmentConverter CreateConverter(
        ILogger<SlackAttachmentConverter>? logger = null)
        => new(new TestSlackFileUploader(), logger);

    private sealed class TestSlackFileUploader : ISlackFileUploader
    {
        private readonly Func<UploadCall, CancellationToken, Task<string?>>? _upload;

        internal TestSlackFileUploader(
            Func<UploadCall, CancellationToken, Task<string?>>? upload = null)
        {
            _upload = upload;
        }

        internal List<UploadCall> Calls { get; } = [];

        public Task<string?> UploadAsync(
            byte[] content,
            string fileName,
            string channel,
            string token,
            CancellationToken cancellationToken)
        {
            var call = new UploadCall(content, fileName, channel, token);
            Calls.Add(call);
            return _upload?.Invoke(call, cancellationToken)
                ?? Task.FromResult<string?>(null);
        }
    }

    private sealed record UploadCall(
        byte[] Content,
        string FileName,
        string Channel,
        string Token);

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        internal List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
        }
    }

    private sealed class ThrowingLogger<T> : ILogger<T>
    {
        private readonly Exception _exception;

        internal ThrowingLogger(Exception exception)
        {
            _exception = exception;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => throw _exception;
    }

    private sealed record LogEntry(
        LogLevel Level,
        string Message,
        Exception? Exception);
}
