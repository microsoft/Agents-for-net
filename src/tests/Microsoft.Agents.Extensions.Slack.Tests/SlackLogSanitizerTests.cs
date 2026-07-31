// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Xunit;

namespace Microsoft.Agents.Extensions.Slack.Tests;

public class SlackLogSanitizerTests
{
    [Fact]
    public void SanitizeJson_RedactsSensitivePropertiesRecursively()
    {
        const string json = """
            {
              "token":"legacy-token",
              "nested":{
                "ApiToken":"xoxb-secret",
                "response_url":"https://hooks.slack.com/actions/secret"
              },
              "items":[
                {"access_token":"access-secret"},
                {"authorization":"******"},
                {"bot_access_token":"bot-secret"},
                {"signing_secret":"signing-secret"}
              ],
              "text":"keep me"
            }
            """;

        var sanitized = SlackLogSanitizer.SanitizeJson(json);

        Assert.Contains("\"text\":\"keep me\"", sanitized);
        Assert.Contains("[REDACTED]", sanitized);
        Assert.DoesNotContain("legacy-token", sanitized);
        Assert.DoesNotContain("xoxb-secret", sanitized);
        Assert.DoesNotContain("hooks.slack.com", sanitized);
        Assert.DoesNotContain("access-secret", sanitized);
        Assert.DoesNotContain("******", sanitized);
        Assert.DoesNotContain("bot-secret", sanitized);
        Assert.DoesNotContain("signing-secret", sanitized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-json")]
    public void SanitizeJson_InvalidJson_ReturnsUnavailable(string json)
    {
        Assert.Equal("[UNAVAILABLE]", SlackLogSanitizer.SanitizeJson(json));
    }

    [Fact]
    public void SanitizeJson_DuplicateProperties_ReturnsUnavailable()
    {
        const string json = """{"token":"first","token":"second"}""";

        var sanitized = SlackLogSanitizer.SanitizeJson(json);

        Assert.Equal("[UNAVAILABLE]", sanitized);
        Assert.DoesNotContain("first", sanitized);
        Assert.DoesNotContain("second", sanitized);
    }
}
