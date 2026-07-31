// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Text.Json.Serialization;
using System.Threading;
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
              "client_secret":"client-secret",
              "clientSecret":"camel-client-secret",
              "refresh_token":"refresh-secret",
              "refreshToken":"camel-refresh-secret",
              "password":"password-secret",
              "databasePasswordHash":"password-hash-secret",
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
        Assert.DoesNotContain("client-secret", sanitized);
        Assert.DoesNotContain("camel-client-secret", sanitized);
        Assert.DoesNotContain("refresh-secret", sanitized);
        Assert.DoesNotContain("camel-refresh-secret", sanitized);
        Assert.DoesNotContain("password-secret", sanitized);
        Assert.DoesNotContain("password-hash-secret", sanitized);
    }

    [Fact]
    public void SanitizeJson_RedactsCredentialBearingUrlsAndPreservesOrdinaryUrlsRecursively()
    {
        const string json = """
            {
              "incoming_webhook":{
                "url":"https://hooks.slack.com/services/T000/B000/webhook-secret"
              },
              "items":[
                {
                  "download_url":"https://files.example.com/report.csv?X-Amz-Signature=signed-secret&expires=123"
                },
                {"token_url":"https://files.example.com/report.csv?access_token=token-secret"},
                {"secret_url":"https://files.example.com/report.csv?client-secret=client-secret-value"},
                {"sig_url":"https://files.example.com/report.csv?request-sig=sig-secret"},
                {"key_url":"https://files.example.com/report.csv?api_key=key-secret"},
                {"authorization_url":"https://files.example.com/report.csv?authorization=authorization-secret"},
                {"password_url":"https://files.example.com/report.csv?database_password_hash=password-secret"},
                {
                  "documentation_url":"https://api.slack.com/messaging/webhooks"
                }
              ]
            }
            """;

        var sanitized = SlackLogSanitizer.SanitizeJson(json);

        Assert.DoesNotContain("webhook-secret", sanitized);
        Assert.DoesNotContain("signed-secret", sanitized);
        Assert.DoesNotContain("token-secret", sanitized);
        Assert.DoesNotContain("client-secret-value", sanitized);
        Assert.DoesNotContain("sig-secret", sanitized);
        Assert.DoesNotContain("key-secret", sanitized);
        Assert.DoesNotContain("authorization-secret", sanitized);
        Assert.DoesNotContain("password-secret", sanitized);
        Assert.Contains("\"url\":\"[REDACTED]\"", sanitized);
        Assert.Contains("\"download_url\":\"[REDACTED]\"", sanitized);
        Assert.Contains("\"documentation_url\":\"https://api.slack.com/messaging/webhooks\"", sanitized);
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

    [Fact]
    public void SanitizeObject_NonSerializableValue_ReturnsUnavailable()
    {
        var sanitized = SlackLogSanitizer.SanitizeObject(new { Value = typeof(string) });

        Assert.Equal("[UNAVAILABLE]", sanitized);
    }

    [Fact]
    public void SanitizeObject_CollidingJsonPropertyNames_ReturnsUnavailable()
    {
        var sanitized = SlackLogSanitizer.SanitizeObject(new CollidingJsonProperties());

        Assert.Equal("[UNAVAILABLE]", sanitized);
    }

    [Fact]
    public void SanitizeObject_PropertyGetterThrows_ReturnsUnavailable()
    {
        var sanitized = SlackLogSanitizer.SanitizeObject(new ThrowingJsonProperty());

        Assert.Equal("[UNAVAILABLE]", sanitized);
    }

    [Fact]
    public void SanitizeObject_PropertyGetterThrowsOperationCanceledException_Propagates()
    {
        Assert.Throws<OperationCanceledException>(
            () => SlackLogSanitizer.SanitizeObject(new CancelingJsonProperty()));
    }

    [Fact]
    public void SanitizeObject_PropertyGetterThrowsThreadInterruptedException_Propagates()
    {
        Assert.Throws<ThreadInterruptedException>(
            () => SlackLogSanitizer.SanitizeObject(new ThreadInterruptedJsonProperty()));
    }

#pragma warning disable CS0618
    [Fact]
    public void SanitizeObject_PropertyGetterThrowsExecutionEngineException_Propagates()
    {
        Assert.Throws<ExecutionEngineException>(
            () => SlackLogSanitizer.SanitizeObject(new ExecutionEngineJsonProperty()));
    }
#pragma warning restore CS0618
}

internal sealed class CollidingJsonProperties
{
    [JsonPropertyName("value")]
    public string First { get; } = "first";

    [JsonPropertyName("value")]
    public string Second { get; } = "second";
}

internal sealed class ThrowingJsonProperty
{
    public string Value => throw new ApplicationException("Getter failure");
}

internal sealed class CancelingJsonProperty
{
    public string Value => throw new OperationCanceledException("Getter canceled");
}

internal sealed class ThreadInterruptedJsonProperty
{
    public string Value => throw new ThreadInterruptedException("Getter interrupted");
}

#pragma warning disable CS0618
internal sealed class ExecutionEngineJsonProperty
{
    public string Value => throw new ExecutionEngineException("Getter failed fatally");
}
#pragma warning restore CS0618
