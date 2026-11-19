// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.Testing.Http;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Testing;

public class HttpSimulationRedactionTests
{
    [Theory]
    [InlineData("application/x-www-form-urlencoded")]
    [InlineData("Application/X-WWW-Form-Urlencoded")]
    [InlineData("APPLICATION/X-WWW-FORM-URLENCODED")]
    public async Task CapturedForm_RedactsCredentialsRegardlessOfMediaTypeCase(string mediaType)
    {
        using var http = new HttpSimulation(new HttpSimulationOptions { CaptureRequestBody = true });
        http.Expect(HttpMethod.Post, "https://tokens.example.test/token").Respond(HttpStatusCode.OK);
        using var client = http.CreateClient();
        using var content = new StringContent("access_token=synthetic-secret&scope=profile", Encoding.UTF8, mediaType);
        using var response = await client.PostAsync("https://tokens.example.test/token", content);

        var exchange = Assert.Single(http.Exchanges);
        Assert.DoesNotContain("synthetic-secret", exchange.Body);
        Assert.Contains("scope=profile", exchange.Body);
        http.Verify();
    }

    [Theory]
    [InlineData("application/json")]
    [InlineData("APPLICATION/JSON")]
    [InlineData("application/problem+json")]
    [InlineData("Application/Vnd.Service+Json")]
    public async Task CapturedMalformedJson_DoesNotExposeCredentialValues(string mediaType)
    {
        using var http = new HttpSimulation(new HttpSimulationOptions { CaptureRequestBody = true });
        http.Expect(HttpMethod.Post, "https://tokens.example.test/token").Respond(HttpStatusCode.OK);
        using var client = http.CreateClient();
        using var content = new StringContent("""{"password":"synthetic-secret",}""", Encoding.UTF8, mediaType);
        using var response = await client.PostAsync("https://tokens.example.test/token", content);

        var body = Assert.Single(http.Exchanges).Body;
        Assert.False(string.IsNullOrEmpty(body));
        Assert.DoesNotContain("synthetic-secret", body);
        http.Verify();
    }

    [Fact]
    public async Task CapturedNonJsonText_PreservesTheOriginalBody()
    {
        const string text = "Diagnostic text, not JSON: {";
        using var http = new HttpSimulation(new HttpSimulationOptions { CaptureRequestBody = true });
        http.Expect(HttpMethod.Post, "https://service.example.test/messages").Respond(HttpStatusCode.OK);
        using var client = http.CreateClient();
        using var content = new StringContent(text, Encoding.UTF8, "text/plain");
        using var response = await client.PostAsync("https://service.example.test/messages", content);

        Assert.Equal(text, Assert.Single(http.Exchanges).Body);
        http.Verify();
    }

    [Theory]
    [InlineData("form", "device_code")]
    [InlineData("form", "code_verifier")]
    [InlineData("json", "device_code")]
    [InlineData("json", "code_verifier")]
    [InlineData("query", "device_code")]
    [InlineData("query", "code_verifier")]
    public async Task CapturedOAuthCredentials_AreRedactedAcrossRequestFormats(string format, string name)
    {
        const string secret = "synthetic-oauth-credential";
        var url = "https://tokens.example.test/token";
        if (format == "query")
        {
            url += $"?{name}={secret}&scope=profile";
        }
        using var http = new HttpSimulation(new HttpSimulationOptions { CaptureRequestBody = true });
        http.Expect(HttpMethod.Post, url).Respond(HttpStatusCode.OK);
        using var client = http.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        if (format == "form")
        {
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                [name] = secret,
                ["scope"] = "profile"
            });
        }
        else if (format == "json")
        {
            request.Content = new StringContent(JsonSerializer.Serialize(new Dictionary<string, string>
            {
                [name] = secret,
                ["scope"] = "profile"
            }), Encoding.UTF8, "application/json");
        }
        using var response = await client.SendAsync(request);

        var exchange = Assert.Single(http.Exchanges);
        var captured = format == "query" ? exchange.RequestUri.AbsoluteUri : exchange.Body;
        Assert.False(string.IsNullOrEmpty(captured));
        Assert.DoesNotContain(secret, captured);
        Assert.Contains("profile", captured);
        http.Verify();
    }
}
