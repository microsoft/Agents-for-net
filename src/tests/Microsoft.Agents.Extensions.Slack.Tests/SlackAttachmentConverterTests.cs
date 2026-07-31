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
    public async Task ConvertAsync_UnknownAttachment_ReturnsEmptyWithoutUploading()
    {
        var uploader = new TestSlackFileUploader();
        var converter = new SlackAttachmentConverter(uploader);

        var result = await converter.ConvertAsync(
            [new Attachment("application/octet-stream", content: new byte[] { 1, 2, 3 })],
            CallbackId,
            Channel,
            Token,
            renderButtonsAsMenu: false,
            CancellationToken.None);

        Assert.Empty(result);
        Assert.Equal(0, uploader.CallCount);
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
        internal int CallCount { get; private set; }

        public Task<string?> UploadAsync(
            byte[] content,
            string fileName,
            string channel,
            string token,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult<string?>(null);
        }
    }

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

    private sealed record LogEntry(
        LogLevel Level,
        string Message,
        Exception? Exception);
}
