// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Teams.Apps;
using System;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Xunit;
using CitationIcons = Microsoft.Teams.Apps.Schema.Entities.CitationIcons;
using PlanStepStatuses = Microsoft.Teams.Apps.Schema.Entities.PlanStepStatuses;
using StepSource = Microsoft.Teams.Apps.Schema.Entities.StepSource;
using UpstreamPlan = Microsoft.Teams.Apps.Plan;

#pragma warning disable ExperimentalTeamsPlan // Compare the opted-in native API with the actual upstream implementation.

namespace Microsoft.Agents.Extensions.MSTeams.Tests;

public partial class TeamsPlanTests
{
    [Theory]
    [InlineData("start", false)]
    [InlineData("continue", false)]
    [InlineData("stop", false)]
    [InlineData("error", false)]
    [InlineData("complete", false)]
    [InlineData("start", true)]
    [InlineData("continue", true)]
    [InlineData("stop", true)]
    [InlineData("error", true)]
    [InlineData("complete", true)]
    public async Task Lifecycle_ComparedWithActualUpstreamWireOutput(string operation, bool chatThread)
    {
        using var native = new PlanEnvironment();
        using var upstream = new PlanEnvironment();
        await upstream.Context.SetTeamsApiClient(NullLoggerFactory.Instance);
        var binding = new PlanOptions
        {
            ConversationId = "conversation",
            ReplyToId = "original-prompt",
            ReplyChain = chatThread ? new PlanReplyChain { ChatOrChannelId = "chat/id", ReplyChainId = "root/id" } : null
        };
        var nativePlan = TeamsAgentExtension.CreatePlan(native.Context, binding);
        var upstreamPlan = new UpstreamPlan(upstream.Context.GetTeamsApiClient(), binding);
        var step = NewStep();
        step.Status = PlanStepStatuses.Blocked;
        step.Version = 7;
        step.Description = "Waiting for the example source.";
        step.Sources =
        [
            new StepSource
            {
                Title = "Reference",
                Url = new Uri("https://example.com/reference"),
                Icon = CitationIcons.Text
            }
        ];

        switch (operation)
        {
            case "start":
                await nativePlan.StartAsync([step]);
                await upstreamPlan.StartAsync([step]);
                break;
            case "continue":
                await nativePlan.UpdateAsync([step]);
                await upstreamPlan.UpdateAsync([step]);
                break;
            case "stop":
                await nativePlan.StopAsync();
                await upstreamPlan.StopAsync();
                break;
            case "error":
                await nativePlan.ErrorAsync();
                await upstreamPlan.ErrorAsync();
                break;
            case "complete":
                await nativePlan.CompleteAsync("Answer.");
                await upstreamPlan.CompleteAsync("Answer.");
                break;
        }

        var actual = Assert.Single(native.Requests);
        var expected = Assert.Single(upstream.Requests);
        Assert.Equal(expected.Method, actual.Method);
        Assert.Equal(expected.Uri.AbsoluteUri.TrimEnd('/'), actual.Uri.AbsoluteUri.TrimEnd('/'));
        var expectedBody = JsonNode.Parse(expected.Body)!.AsObject();
        var actualBody = WithoutCoreEnvelope(actual);
        Assert.True(JsonNode.DeepEquals(expectedBody, actualBody),
            $"Upstream: {expectedBody}\nNative: {actualBody}");
    }

    private static void SortEntities(JsonObject activity)
    {
        activity["entities"] = new JsonArray(activity["entities"]!.AsArray()
            .OrderBy(entity => entity!["type"]!.GetValue<string>())
            .Select(entity => entity!.DeepClone()).ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StreamingCompletion_ComparedWithActualUpstreamWireOutput(bool chatThread)
    {
        using var native = new PlanEnvironment();
        using var upstream = new PlanEnvironment();
        await upstream.Context.SetTeamsApiClient(NullLoggerFactory.Instance);
        var binding = new PlanOptions
        {
            ConversationId = "conversation",
            ReplyToId = "prompt",
            ReplyChain = chatThread ? new PlanReplyChain { ChatOrChannelId = "chat/id", ReplyChainId = "root/id" } : null
        };
        var nativePlan = TeamsAgentExtension.CreatePlan(native.Context, binding);
        var upstreamPlan = new UpstreamPlan(upstream.Context.GetTeamsApiClient(), binding);
        using var httpClient = upstream.CreateHttpClient();
        var client = new Microsoft.Teams.Core.ConversationClient(
            httpClient, NullLogger<Microsoft.Teams.Core.ConversationClient>.Instance);
        var writer = CreateUpstreamWriter(client, upstream.Context.Activity, chatThread);

        await native.Context.StreamingResponse.QueueInformativeUpdateAsync("Working.");
        await writer.SendInformativeUpdateAsync("Working.");
        native.Context.StreamingResponse.QueueTextChunk("Answer.");
        await writer.AppendResponseAsync("Answer.");
        Assert.Equal(StreamingResponseResult.Success, await nativePlan.CompleteStreamingAsync());
        await writer.FinalizeResponseAsync(upstreamPlan.Completion());

        AssertEquivalentStreamingUpdate(upstream.Requests[0], native.Requests[0]);
        AssertEquivalentStreamingUpdate(
            Assert.Single(upstream.Requests, request => HasStreamType(request, StreamTypes.Streaming)),
            Assert.Single(native.Requests, request => HasStreamType(request, StreamTypes.Streaming)));

        var actual = Assert.Single(native.Requests, IsMessagePost);
        var expected = Assert.Single(upstream.Requests, IsMessagePost);
        Assert.Equal(expected.Uri.AbsoluteUri.TrimEnd('/'), actual.Uri.AbsoluteUri.TrimEnd('/'));
        var expectedBody = JsonNode.Parse(expected.Body)!.AsObject();
        var actualBody = WithoutCoreEnvelope(actual);
        var streamInfo = actualBody["entities"]!.AsArray()
            .Single(entity => entity!["type"]!.GetValue<string>() == "streaminfo")!.AsObject();
        Assert.Equal("success", streamInfo["streamResult"]!.GetValue<string>());
        // Core adds its shared stream-result field; entity ordering is not part of the Activity contract.
        streamInfo.Remove("streamResult");
        SortEntities(expectedBody);
        SortEntities(actualBody);
        Assert.True(JsonNode.DeepEquals(expectedBody, actualBody),
            $"Upstream: {expectedBody}\nNative: {actualBody}");
    }

    private static bool HasStreamType(RecordedRequest request, string streamType)
    {
        var body = JsonNode.Parse(request.Body)!.AsObject();
        return body["entities"]!.AsArray().Any(entity => entity!["type"]!.GetValue<string>() == "streaminfo"
            && entity["streamType"]!.GetValue<string>() == streamType);
    }

    private static void AssertEquivalentStreamingUpdate(RecordedRequest expectedRequest, RecordedRequest actualRequest)
    {
        var expected = JsonNode.Parse(expectedRequest.Body)!.AsObject();
        var actual = WithoutCoreEnvelope(actualRequest);
        var expectedStream = expected["entities"]!.AsArray()
            .Single(entity => entity!["type"]!.GetValue<string>() == "streaminfo")!.AsObject();
        var actualStream = actual["entities"]!.AsArray()
            .Single(entity => entity!["type"]!.GetValue<string>() == "streaminfo")!.AsObject();

        // Upstream mirrors the canonical streaminfo fields into legacy channelData; Core uses the entity.
        if (expected["channelData"] is JsonObject channelData)
        {
            foreach (var name in new[] { "streamId", "streamType", "streamSequence" })
            {
                if (channelData.TryGetPropertyValue(name, out var value))
                {
                    Assert.True(JsonNode.DeepEquals(value, expectedStream[name]));
                    channelData.Remove(name);
                }
            }
            if (channelData.Count == 0)
            {
                expected.Remove("channelData");
            }
        }
        if (expectedStream["streamId"] == null && !actualStream.ContainsKey("streamId"))
        {
            expectedStream.Remove("streamId");
        }
        SortEntities(expected);
        SortEntities(actual);
        Assert.True(JsonNode.DeepEquals(expected, actual),
            $"Upstream stream update: {expected}\nNative stream update: {actual}");
    }

    private static JsonObject WithoutCoreEnvelope(RecordedRequest request)
    {
        var body = JsonNode.Parse(request.Body)!.AsObject();
        Assert.Equal("https://regional.example/teams/", body["serviceUrl"]!.GetValue<string>());
        Assert.Equal("msteams", body["channelId"]!.GetValue<string>());
        Assert.Equal("agent", body["from"]!["id"]!.GetValue<string>());
        Assert.Equal("conversation", body["conversation"]!["id"]!.GetValue<string>());
        Assert.Equal("user", body["recipient"]!["id"]!.GetValue<string>());
        foreach (var name in new[] { "serviceUrl", "channelId", "from", "conversation", "recipient" })
        {
            body.Remove(name);
        }

        // Core sends its addressing envelope and eagerly initialized empty collections.
        foreach (var name in new[]
        {
            "membersAdded", "membersRemoved", "reactionsAdded", "reactionsRemoved",
            "attachments", "listenFor", "textHighlights"
        })
        {
            if (body[name] is JsonArray { Count: 0 })
            {
                body.Remove(name);
            }
        }
        return body;
    }

    private static TeamsStreamingWriter CreateUpstreamWriter(
        Microsoft.Teams.Core.ConversationClient client, IActivity activity, bool chatThread)
    {
        var assembly = typeof(TeamsStreamingWriter).Assembly;
        object options = null;
        if (chatThread)
        {
            var placementType = assembly.GetType("Microsoft.Teams.Apps.ReplyChainPlacement", throwOnError: true);
            var optionsType = assembly.GetType("Microsoft.Teams.Apps.TeamsStreamingWriterOptions", throwOnError: true);
            var placement = Activator.CreateInstance(placementType, "chat/id", "root/id", false);
            options = Activator.CreateInstance(optionsType, placement);
        }

        // The upstream writer constructor is internal; the oracle still executes the real package.
        var constructor = typeof(TeamsStreamingWriter).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(candidate => candidate.GetParameters().Length == 4);
        return (TeamsStreamingWriter)constructor.Invoke([client, activity.ToTeamsActivity(), NullLogger.Instance, options]);
    }
}

#pragma warning restore ExperimentalTeamsPlan
