// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable enable

using Microsoft.Agents.Core.Models;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Extensions.Slack.Tests;

public class SlackMessageConverterTests
{
    private readonly SlackMessageConverter _converter = new(
        new SlackAttachmentConverter(
            new TestSlackFileUploader()));

    [Fact]
    public async Task ConvertAsync_TextAndSuggestedActions_ReturnsOrderedThreadedPayloads()
    {
        var activity = new Activity
        {
            Type = ActivityTypes.Message,
            Text = "A & B < C > D",
            SuggestedActions = new SuggestedActions(
                actions:
                [
                    new CardAction(ActionTypes.ImBack, title: "First title", value: "First value"),
                    new CardAction(ActionTypes.ImBack, title: "Second title"),
                ]),
        };

        var payloads = await _converter.ConvertAsync(
            activity,
            "C123",
            "123.456",
            "xoxb-token",
            CancellationToken.None);

        Assert.Collection(
            payloads,
            payload =>
            {
                Assert.Equal("C123", payload.Channel);
                Assert.Equal("A &amp; B &lt; C &gt; D", payload.Text);
                Assert.Equal("123.456", payload.ThreadTs);
                Assert.Null(payload.Attachments);
            },
            payload =>
            {
                Assert.Equal("C123", payload.Channel);
                Assert.Equal("* First value\n\n* Second title", payload.Text);
                Assert.Equal("123.456", payload.ThreadTs);
                Assert.Null(payload.Attachments);
            });
    }

    [Fact]
    public async Task ConvertAsync_SuggestedActionsOnly_ReturnsSuggestionPayload()
    {
        var activity = new Activity
        {
            Type = ActivityTypes.Message,
            SuggestedActions = new SuggestedActions(
                actions: [new CardAction(ActionTypes.ImBack, title: "Choose", value: "Choice")]),
        };

        var payload = Assert.Single(await _converter.ConvertAsync(
            activity,
            "C123",
            null,
            "xoxb-token",
            CancellationToken.None));

        Assert.Equal("C123", payload.Channel);
        Assert.Equal("* Choice", payload.Text);
        Assert.Null(payload.ThreadTs);
    }

    [Fact]
    public async Task ConvertAsync_MessageBack_UsesTextBeforeValueAndTitle()
    {
        var activity = new Activity
        {
            Type = ActivityTypes.Message,
            SuggestedActions = new SuggestedActions(
                actions:
                [
                    new CardAction(
                        ActionTypes.MessageBack,
                        title: "Title",
                        text: "Message text",
                        value: "Value"),
                ]),
        };

        var payload = Assert.Single(await _converter.ConvertAsync(
            activity,
            "C123",
            null,
            "xoxb-token",
            CancellationToken.None));

        Assert.Equal("* Message text", payload.Text);
    }

    [Theory]
    [InlineData(ActionTypes.MessageBack)]
    [InlineData(ActionTypes.PostBack)]
    public async Task ConvertAsync_ActionWithoutUsablePreferredValue_FallsBackToTitle(string actionType)
    {
        var activity = new Activity
        {
            Type = ActivityTypes.Message,
            SuggestedActions = new SuggestedActions(
                actions: [new CardAction(actionType, title: "Fallback title", value: new { Id = 1 })]),
        };

        var payload = Assert.Single(await _converter.ConvertAsync(
            activity,
            "C123",
            null,
            "xoxb-token",
            CancellationToken.None));

        Assert.Equal("* Fallback title", payload.Text);
    }

    [Fact]
    public async Task ConvertAsync_NonMessageActivity_ReturnsNoPayloads()
    {
        var activity = new Activity
        {
            Type = ActivityTypes.Event,
            Text = "Ignored",
            SuggestedActions = new SuggestedActions(
                actions: [new CardAction(ActionTypes.ImBack, title: "Ignored", value: "Ignored")]),
        };

        var payloads = await _converter.ConvertAsync(
            activity,
            "C123",
            "123.456",
            "xoxb-token",
            CancellationToken.None);

        Assert.Empty(payloads);
    }

    [Fact]
    public async Task ConvertAsync_EmptyMessageActivity_ReturnsNoPayloads()
    {
        var activity = new Activity
        {
            Type = ActivityTypes.Message,
        };

        var payloads = await _converter.ConvertAsync(
            activity,
            "C123",
            "123.456",
            "xoxb-token",
            CancellationToken.None);

        Assert.Empty(payloads);
    }

    private sealed class TestSlackFileUploader : ISlackFileUploader
    {
        public Task<string?> UploadAsync(
            byte[] content,
            string fileName,
            string channel,
            string token,
            CancellationToken cancellationToken)
            => Task.FromResult<string?>(null);
    }
}
