using System;
using System.Text.Json;

public sealed class AdaptiveCardLinkExtractorTests
{
    [Fact]
    public void Extract_ReturnsAllNestedOpenUrls()
    {
        const string card = """
            {
              "type": "AdaptiveCard",
              "body": [
                {
                  "type": "ActionSet",
                  "actions": [
                    { "type": "Action.OpenUrl", "title": "Sign in", "url": "https://login.example/" }
                  ]
                }
              ],
              "actions": [
                { "type": "Action.OpenUrl", "title": "Help", "url": "https://help.example/" }
              ]
            }
            """;

        LinkExtractionResult result = AdaptiveCardLinkExtractor.Extract(card);

        Assert.Null(result.Error);
        Assert.Equal(
            [
                new ChatLink("Sign in", new Uri("https://login.example/")),
                new ChatLink("Help", new Uri("https://help.example/"))
            ],
            result.Links);
    }

    [Fact]
    public void Extract_ActionFreeCard_IsSuccessfulWithNoLinks()
    {
        JsonElement card = JsonSerializer.SerializeToElement(new
        {
            type = "AdaptiveCard",
            body = new object[]
            {
                new { type = "TextBlock", text = "Authentication status" },
                new { type = "FactSet", facts = new[] { new { title = "State", value = "Ready" } } }
            }
        });

        LinkExtractionResult result = AdaptiveCardLinkExtractor.Extract(card);

        Assert.Empty(result.Links);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Extract_IgnoresOpenUrlActionWithNonStringType()
    {
        const string card = """
            {
              "type": "AdaptiveCard",
              "actions": [
                { "type": 123, "title": "Broken", "url": "https://broken.example/" },
                { "type": "Action.OpenUrl", "title": "Help", "url": "https://help.example/" }
              ]
            }
            """;

        LinkExtractionResult result = AdaptiveCardLinkExtractor.Extract(card);

        Assert.Null(result.Error);
        Assert.Equal([new ChatLink("Help", new Uri("https://help.example/"))], result.Links);
    }

    [Fact]
    public void Extract_IgnoresNestedOpenUrlActionWithNonStringUrl()
    {
        const string card = """
            {
              "type": "AdaptiveCard",
              "body": [
                {
                  "type": "ActionSet",
                  "actions": [
                    { "type": "Action.OpenUrl", "title": "Broken", "url": 123 }
                  ]
                }
              ],
              "actions": [
                { "type": "Action.OpenUrl", "title": "Help", "url": "https://help.example/" }
              ]
            }
            """;

        LinkExtractionResult result = AdaptiveCardLinkExtractor.Extract(card);

        Assert.Null(result.Error);
        Assert.Equal([new ChatLink("Help", new Uri("https://help.example/"))], result.Links);
    }

    [Fact]
    public void Extract_UsesUrlWhenOpenUrlTitleIsNotString()
    {
        const string card = """
            {
              "type": "AdaptiveCard",
              "actions": [
                { "type": "Action.OpenUrl", "title": {}, "url": "https://docs.example/" }
              ]
            }
            """;

        LinkExtractionResult result = AdaptiveCardLinkExtractor.Extract(card);

        Assert.Null(result.Error);
        Assert.Equal([new ChatLink("https://docs.example/", new Uri("https://docs.example/"))], result.Links);
    }

    [Fact]
    public void Extract_RuntimeObject_UsesProtocolSerialization()
    {
        object card = new
        {
            type = "AdaptiveCard",
            actions = new[]
            {
                new { type = "Action.OpenUrl", title = "Docs", url = "https://docs.example/" }
            }
        };

        LinkExtractionResult result = AdaptiveCardLinkExtractor.Extract(card);

        Assert.Null(result.Error);
        Assert.Equal([new ChatLink("Docs", new Uri("https://docs.example/"))], result.Links);
    }

    [Fact]
    public void Extract_MalformedJson_ReturnsError()
    {
        LinkExtractionResult result = AdaptiveCardLinkExtractor.Extract("{not-json");

        Assert.Empty(result.Links);
        Assert.NotNull(result.Error);
    }
}
