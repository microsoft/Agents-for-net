// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Tests;

public class StreamingResponseFinalDeliveryTests
{
    [Fact]
    public async Task BufferedFinal_UsesCustomDeliveryWithComposedMetadata()
    {
        var sent = new List<IActivity>();
        using var context = CreateContext(Channels.Test, activities => sent.AddRange(activities));
        context.StreamingResponse.EnableGeneratedByAILabel = true;
        context.StreamingResponse.SensitivityLabel = new SensitivityUsageInfo { Name = "Internal" };
        context.StreamingResponse.AddAttachment(new Attachment { ContentType = "text/plain", Content = "attachment" });
        context.StreamingResponse.AddCitation(new Citation("Reference text", "Reference", "https://example.com/reference"), 1);
        context.StreamingResponse.QueueTextChunk("Final answer [doc1].");
        IActivity delivered = null;
        using var cancellation = new CancellationTokenSource();

        var result = await context.StreamingResponse.EndStreamAsync((activity, token) =>
        {
            Assert.Equal(cancellation.Token, token);
            delivered = activity;
            return Task.FromResult(new ResourceResponse("final"));
        }, cancellation.Token);

        Assert.Equal(StreamingResponseResult.Success, result);
        Assert.Empty(sent);
        Assert.Equal(ActivityTypes.Message, delivered.Type);
        Assert.Equal("Final answer [1].", delivered.Text);
        Assert.Single(delivered.Attachments);
        var entity = Assert.Single(delivered.Entities.OfType<AIEntity>());
        Assert.Equal("Internal", entity.UsageInfo.Name);
        Assert.Equal("Reference", Assert.Single(entity.Citation).Appearance.Name);
        Assert.Contains("AIGeneratedContent", entity.AdditionalType);
        Assert.Null(delivered.GetStreamingEntity());
    }

    [Fact]
    public async Task StreamingFinal_UsesCustomDeliveryWithAssignedStreamId()
    {
        var sent = new List<IActivity>();
        using var context = CreateContext(Channels.Msteams, activities => sent.AddRange(activities));
        await context.StreamingResponse.QueueInformativeUpdateAsync("Working.");
        context.StreamingResponse.QueueTextChunk("Final answer.");
        IActivity delivered = null;

        var result = await context.StreamingResponse.EndStreamAsync((activity, _) =>
        {
            delivered = activity;
            return Task.FromResult(new ResourceResponse("final"));
        });

        Assert.Equal(StreamingResponseResult.Success, result);
        Assert.All(sent, activity => Assert.Equal(ActivityTypes.Typing, activity.Type));
        Assert.Equal("Final answer.", delivered.Text);
        Assert.Equal("stream-id", delivered.Id);
        var streamInfo = Assert.IsType<StreamInfo>(delivered.GetStreamingEntity());
        Assert.Equal(StreamTypes.Final, streamInfo.StreamType);
        Assert.Equal("stream-id", streamInfo.StreamId);
    }

    [Fact]
    public async Task EarlierServiceTimeout_UsesCustomFinalDeliveryInsteadOfUpdating()
    {
        using var context = CreateContext(Channels.Msteams, _ => throw new ErrorResponseException("Stream timeout.")
        {
            Body = new ErrorResponse(new Error
            {
                Code = "ContentStreamNotAllowed",
                Message = "Content stream finished due to exceeded streaming time."
            })
        });
        await context.StreamingResponse.QueueInformativeUpdateAsync("Working.");
        context.StreamingResponse.QueueTextChunk("Recovered answer.");
        IActivity delivered = null;

        var result = await context.StreamingResponse.EndStreamAsync((activity, _) =>
        {
            delivered = activity;
            return Task.FromResult(new ResourceResponse("recovered"));
        });

        Assert.Equal(StreamingResponseResult.Success, result);
        Assert.Equal("Recovered answer.", delivered.Text);
        Assert.Null(delivered.GetStreamingEntity());
    }

    [Theory]
    [InlineData(Channels.Test)]
    [InlineData(Channels.Msteams)]
    public async Task CustomDeliveryFailure_PropagatesToEndingCaller(string channelId)
    {
        using var context = CreateContext(channelId);
        await context.StreamingResponse.QueueInformativeUpdateAsync("Working.");
        context.StreamingResponse.QueueTextChunk("Final answer.");
        var failure = new InvalidOperationException("Delivery failed.");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.StreamingResponse.EndStreamAsync((_, _) => Task.FromException<ResourceResponse>(failure)));

        Assert.Same(failure, exception);
    }

    [Theory]
    [InlineData(Channels.Test)]
    [InlineData(Channels.Msteams)]
    public async Task CanceledFinalDelivery_DoesNotInvokeSender(string channelId)
    {
        using var context = CreateContext(channelId);
        await context.StreamingResponse.QueueInformativeUpdateAsync("Working.");
        context.StreamingResponse.QueueTextChunk("Final answer.");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var deliveries = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            context.StreamingResponse.EndStreamAsync((_, _) =>
            {
                deliveries++;
                return Task.FromResult(new ResourceResponse("final"));
            }, cancellation.Token));

        Assert.Equal(0, deliveries);
    }

    [Fact]
    public async Task RepeatedEnding_DoesNotRedeliverAndResetDoesNotRetainSender()
    {
        using var context = CreateContext(Channels.Test);
        var firstDeliveries = 0;
        context.StreamingResponse.QueueTextChunk("First.");
        Func<IActivity, CancellationToken, Task<ResourceResponse>> sender = (_, _) =>
        {
            firstDeliveries++;
            return Task.FromResult(new ResourceResponse("first"));
        };
        Assert.Equal(StreamingResponseResult.Success, await context.StreamingResponse.EndStreamAsync(sender));
        Assert.Equal(StreamingResponseResult.AlreadyEnded, await context.StreamingResponse.EndStreamAsync(sender));

        await context.StreamingResponse.ResetAsync();
        context.StreamingResponse.QueueTextChunk("Second.");
        IActivity second = null;
        Assert.Equal(StreamingResponseResult.Success, await context.StreamingResponse.EndStreamAsync((activity, _) =>
        {
            second = activity;
            return Task.FromResult(new ResourceResponse("second"));
        }));

        Assert.Equal(1, firstDeliveries);
        Assert.Equal("Second.", second.Text);
    }

    [Fact]
    public async Task EmptyBufferedResponse_DoesNotInvokeCustomSender()
    {
        using var context = CreateContext(Channels.Test);
        var result = await context.StreamingResponse.EndStreamAsync((_, _) =>
            throw new InvalidOperationException("There is no final payload to deliver."));

        Assert.Equal(StreamingResponseResult.Success, result);
    }

    [Fact]
    public async Task NullFinalSender_DoesNotConsumeDefaultCompletion()
    {
        var sent = new List<IActivity>();
        using var context = CreateContext(Channels.Test, activities => sent.AddRange(activities));
        context.StreamingResponse.QueueTextChunk("Answer.");

        var error = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            context.StreamingResponse.EndStreamAsync(
                (Func<IActivity, CancellationToken, Task<ResourceResponse>>)null));

        Assert.Equal("finalActivitySender", error.ParamName);
        Assert.Equal(StreamingResponseResult.Success, await context.StreamingResponse.EndStreamAsync());
        Assert.Equal("Answer.", Assert.Single(sent).Text);
    }

    private static TurnContext CreateContext(string channelId, Action<IActivity[]> onSend = null)
    {
        var adapter = new SimpleAdapter((IActivity[] activities) =>
        {
            foreach (var activity in activities)
            {
                activity.Id = "stream-id";
            }
            onSend?.Invoke(activities);
        });
        var context = new TurnContext(adapter, new Activity { Type = ActivityTypes.Message, ChannelId = channelId });
        context.StreamingResponse.InitialDelay = 1;
        context.StreamingResponse.Interval = 1;
        context.StreamingResponse.EndStreamTimeout = 5000;
        return context;
    }
}
