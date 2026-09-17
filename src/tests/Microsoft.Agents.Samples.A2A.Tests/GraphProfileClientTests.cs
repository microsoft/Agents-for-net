extern alias A2AAgentSample;

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using A2AAgentSample::A2AAgent;

namespace Microsoft.Agents.Samples.A2A.Tests;

public class GraphProfileClientTests
{
    [Fact]
    public async Task GetMeAsync_ReadsMailAndFallbackFields()
    {
        var handler = new RecordingJsonHandler("""
            { "displayName": "Ada Lovelace", "mail": "ada@example.com", "userPrincipalName": "ada@contoso.com" }
            """);
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://graph.microsoft.com/v1.0/")
        };

        var sut = new GraphProfileClient(client);
        GraphProfile profile = await sut.GetMeAsync("graph-token", CancellationToken.None);

        Assert.EndsWith(
            "me?$select=displayName,mail,userPrincipalName",
            handler.LastRequest!.RequestUri!.ToString(),
            StringComparison.Ordinal);

        Assert.Equal("ada@example.com", profile.Mail);
        Assert.Equal("ada@example.com", profile.MailOrUserPrincipalName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MailOrUserPrincipalName_FallsBackToUserPrincipalName_WhenMailIsNullOrWhitespace(string? mail)
    {
        var profile = new GraphProfile("Ada Lovelace", mail, "ada@contoso.com");

        Assert.Equal("ada@contoso.com", profile.MailOrUserPrincipalName);
    }

    private sealed class RecordingJsonHandler(string json) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}
