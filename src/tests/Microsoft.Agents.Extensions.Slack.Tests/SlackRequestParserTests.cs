// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable enable

using System;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Microsoft.Agents.Extensions.Slack.Tests;

public class SlackRequestParserTests
{
    private readonly SlackRequestParser _parser = new();

    [Fact]
    public void Parse_UrlVerification_ReturnsChallenge()
    {
        const string body = """{"type":"url_verification","challenge":"abc123"}""";

        var parsed = _parser.Parse(body, "application/json");

        Assert.Equal(SlackRequestKind.UrlVerification, parsed.Kind);
        Assert.Equal(body, parsed.PayloadJson);
        Assert.Equal("abc123", parsed.Challenge);
        Assert.Null(parsed.EventEnvelope);
        Assert.Null(parsed.ActionPayload);
    }

    [Fact]
    public void Parse_EventCallback_ReturnsEventEnvelope()
    {
        const string body = """
            {
              "type":"event_callback",
              "team_id":"T1",
              "event_id":"Ev123",
              "event":{"type":"message","text":"hello"}
            }
            """;

        var parsed = _parser.Parse(body, "application/json; charset=utf-8");

        Assert.Equal(SlackRequestKind.Event, parsed.Kind);
        Assert.Equal(body, parsed.PayloadJson);
        Assert.Equal("Ev123", parsed.EventEnvelope?.event_id);
        Assert.Equal("hello", parsed.EventEnvelope?.event_content?.text);
        Assert.Null(parsed.Challenge);
        Assert.Null(parsed.ActionPayload);
    }

    [Fact]
    public void Parse_FormEncodedInteractivePayload_ReturnsDecodedPayload()
    {
        const string payload = """
            {"type":"block_actions","team":{"id":"T1"},"channel":{"id":"C1"},"user":{"id":"U1"},"actions":[{"type":"button","value":"hello world"}]}
            """;
        var body = $"payload={WebUtility.UrlEncode(payload)}";

        var parsed = _parser.Parse(body, "application/x-www-form-urlencoded; charset=utf-8");

        Assert.Equal(SlackRequestKind.Interactive, parsed.Kind);
        Assert.Equal(payload, parsed.PayloadJson);
        Assert.Equal("block_actions", parsed.ActionPayload?.type);
        Assert.Equal("C1", parsed.ActionPayload?.channel);
        Assert.Equal("hello world", parsed.ActionPayload?.Get<string>("actions[0].value"));
        Assert.Null(parsed.Challenge);
        Assert.Null(parsed.EventEnvelope);
    }

    [Theory]
    [InlineData("")]
    [InlineData("other=value")]
    [InlineData("payload=")]
    public void Parse_FormWithoutPayload_ReturnsIgnore(string body)
    {
        var parsed = _parser.Parse(body, "application/x-www-form-urlencoded");

        Assert.Equal(SlackRequestKind.Ignore, parsed.Kind);
        Assert.Null(parsed.PayloadJson);
        Assert.Null(parsed.ActionPayload);
    }

    [Theory]
    [InlineData("""{"type":"app_rate_limited"}""")]
    [InlineData("""{"type":"other"}""")]
    [InlineData("""{}""")]
    public void Parse_NonEventEnvelope_ReturnsIgnore(string body)
    {
        var parsed = _parser.Parse(body, "application/json");

        Assert.Equal(SlackRequestKind.Ignore, parsed.Kind);
        Assert.Equal(body, parsed.PayloadJson);
        Assert.Null(parsed.EventEnvelope);
    }

    [Fact]
    public void Parse_MalformedJson_ThrowsJsonException()
    {
        Assert.ThrowsAny<JsonException>(() => _parser.Parse("""{"type":""", "application/json"));
    }
}
