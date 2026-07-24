// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System;
using Xunit;

namespace Microsoft.Agents.Extensions.Slack.Tests;

public class SlackHelpersTests
{
    [Theory]
    [InlineData("Fish & Chips", "Fish &amp; Chips")]
    [InlineData("<tag>", "&lt;tag&gt;")]
    [InlineData("No special chars", "No special chars")]
    public void SlackEncode_EncodesExpectedValue(string input, string expected)
    {
        Assert.Equal(expected, input.SlackEncode());
    }

    [Fact]
    public void SlackEncode_Null_ReturnsNull()
    {
        string input = null;

        Assert.Null(input.SlackEncode());
    }

    [Theory]
    [InlineData("Fish &amp; Chips", "Fish & Chips")]
    [InlineData("&lt;tag&gt;", "<tag>")]
    [InlineData("No special chars", "No special chars")]
    public void SlackDecode_DecodesExpectedValue(string input, string expected)
    {
        Assert.Equal(expected, input.SlackDecode());
    }

    [Fact]
    public void SlackDecode_Null_ReturnsNull()
    {
        string input = null;

        Assert.Null(input.SlackDecode());
    }

    [Fact]
    public void SlackDecode_RoundTripsEncodedValue()
    {
        const string original = "Use & keep <this> safe";

        Assert.Equal(original, original.SlackEncode().SlackDecode());
    }

    [Fact]
    public void CreateConversationId_WithThreadTs_ReturnsFourPartConversationId()
    {
        var conversationId = SlackHelpers.CreateConversationId("B123", "T123", "C123", "12345.6789");

        Assert.Equal("B123:T123:C123:12345.6789", conversationId);
    }

    [Fact]
    public void CreateConversationId_WithoutThreadTs_ReturnsThreePartConversationId()
    {
        var conversationId = SlackHelpers.CreateConversationId("B123", "T123", "C123", null);

        Assert.Equal("B123:T123:C123", conversationId);
    }

    [Fact]
    public void ConversationIdHelpers_ExtractPartsFromThreePartConversationId()
    {
        const string conversationId = "B123:T123:C123";

        Assert.Equal("B123", SlackHelpers.SlackBotIdFromConversationId(conversationId));
        Assert.Equal("T123", SlackHelpers.SlackTeamIdFromConversationId(conversationId));
        Assert.Equal("C123", SlackHelpers.SlackChannelIdFromConversationId(conversationId));
        Assert.Null(SlackHelpers.SlackThreadTsFromConversationId(conversationId));
    }

    [Fact]
    public void ConversationIdHelpers_ExtractThreadTsFromFourPartConversationId()
    {
        const string conversationId = "B123:T123:C123:12345.6789";

        Assert.Equal("12345.6789", SlackHelpers.SlackThreadTsFromConversationId(conversationId));
    }

    [Fact]
    public void ConversationAccountExtensions_ReturnExpectedParts()
    {
        var conversationAccount = new ConversationAccount
        {
            Id = "B123:T123:C123:12345.6789"
        };

        Assert.Equal("B123", conversationAccount.GetSlackBotId());
        Assert.Equal("T123", conversationAccount.GetSlackTeamId());
        Assert.Equal("C123", conversationAccount.GetSlackChannelId());
        Assert.Equal("12345.6789", conversationAccount.GetSlackThreadTs());
    }

    [Fact]
    public void SlackBotIdFromConversationId_NullConversationId_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => SlackHelpers.SlackBotIdFromConversationId(null));

        Assert.Equal("conversationId", exception.ParamName);
    }

    [Theory]
    [InlineData("B123:T123")]
    [InlineData("B123:T123:C123:12345.6789:extra")]
    public void SlackBotIdFromConversationId_InvalidFormat_ThrowsArgumentException(string conversationId)
    {
        var exception = Assert.Throws<ArgumentException>(() => SlackHelpers.SlackBotIdFromConversationId(conversationId));

        Assert.Equal("conversationId", exception.ParamName);
    }
}
