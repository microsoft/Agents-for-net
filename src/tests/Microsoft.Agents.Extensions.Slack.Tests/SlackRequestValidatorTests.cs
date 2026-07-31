// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable enable

using Microsoft.AspNetCore.Http;
using System;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Microsoft.Agents.Extensions.Slack.Tests;

public class SlackRequestValidatorTests
{
    private const string SigningSecret = "8f742231b10e8888abcd99yyyzzz85a5";
    private const string Body = """{"type":"url_verification","challenge":"abc123"}""";

    [Fact]
    public void Verify_ValidSignature_ReturnsTrue()
    {
        var validator = CreateValidator();
        var request = CreateSignedRequest(Body);

        Assert.True(validator.Verify(request, Body));
    }

    [Fact]
    public void Verify_TamperedSignature_ReturnsFalse()
    {
        var validator = CreateValidator();
        var request = CreateSignedRequest(Body);
        request.Headers["X-Slack-Signature"] = "v0=deadbeef";

        Assert.False(validator.Verify(request, Body));
    }

    [Fact]
    public void Verify_StaleTimestamp_ReturnsFalse()
    {
        var validator = CreateValidator(requestMaxAgeSeconds: 300);
        var timestamp = DateTimeOffset.UtcNow.AddSeconds(-301).ToUnixTimeSeconds().ToString();
        var request = CreateSignedRequest(Body, timestamp);

        Assert.False(validator.Verify(request, Body));
    }

    [Fact]
    public void Verify_MissingSignatureHeaders_ReturnsFalse()
    {
        var validator = CreateValidator();
        var request = new DefaultHttpContext().Request;

        Assert.False(validator.Verify(request, Body));
    }

    [Fact]
    public void Verify_EmptySigningSecret_ReturnsTrue()
    {
        var validator = new SlackRequestValidator(new SlackAdapterOptions { SigningSecret = string.Empty });
        var request = new DefaultHttpContext().Request;

        Assert.True(validator.Verify(request, Body));
    }

    private static SlackRequestValidator CreateValidator(int requestMaxAgeSeconds = 300)
        => new(new SlackAdapterOptions
        {
            SigningSecret = SigningSecret,
            RequestMaxAgeSeconds = requestMaxAgeSeconds
        });

    private static HttpRequest CreateSignedRequest(string body, string? timestamp = null)
    {
        timestamp ??= DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var request = new DefaultHttpContext().Request;
        request.Headers["X-Slack-Request-Timestamp"] = timestamp;
        request.Headers["X-Slack-Signature"] = ComputeSignature(timestamp, body);
        return request;
    }

    private static string ComputeSignature(string timestamp, string body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SigningSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"v0:{timestamp}:{body}"));
        var signature = new StringBuilder("v0=");

        foreach (var value in hash)
        {
            signature.Append(value.ToString("x2"));
        }

        return signature.ToString();
    }
}
