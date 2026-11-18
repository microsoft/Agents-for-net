// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using A2A.AspNetCore;
using Microsoft.AspNetCore.Builder;
using ProtocolExtensionAgent;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Microsoft.Agents.Samples.A2A.Tests;

public class ProtocolExtensionSampleTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task DebateOperation_RequiresNegotiatedActivation(bool jsonRpc, bool activate)
    {
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        using var card = JsonDocument.Parse(await client.GetStringAsync("/a2a/card"));
        Assert.Contains(card.RootElement.GetProperty("capabilities").GetProperty("extensions").EnumerateArray(),
            extension => extension.GetProperty("uri").GetString() == DebateExtension.ExtensionUri);
        using var request = new HttpRequestMessage(HttpMethod.Post, jsonRpc ? "/a2a" : "/a2a/debate:resume")
        {
            Content = new StringContent(jsonRpc
                ? """{"jsonrpc":"2.0","id":"1","method":"debate/resume","params":{"topic":"tabs or spaces","argument":"tabs are best"}}"""
                : """{"topic":"tabs or spaces","argument":"tabs are best"}""", Encoding.UTF8, "application/json"),
        };
        if (activate)
        {
            request.Headers.Add("A2A-Extensions", DebateExtension.ExtensionUri);
        }

        using var response = await client.SendAsync(request);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        if (activate)
        {
            response.EnsureSuccessStatusCode();
            var result = jsonRpc ? body.RootElement.GetProperty("result") : body.RootElement;
            Assert.Equal("Counterpoint on tabs or spaces: what if the opposite of 'tabs are best' were true?",
                result.GetProperty("reply").GetString());
        }
        else if (jsonRpc)
        {
            Assert.Equal((int)A2AErrorCode.ExtensionSupportRequired,
                body.RootElement.GetProperty("error").GetProperty("code").GetInt32());
        }
        else
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Theory]
    [InlineData(false, "You said: tabs are best")]
    [InlineData(true, "Debate mode: I respectfully disagree that tabs are best.")]
    public async Task StandardMessage_UsesOptionalExtensionSnapshot(bool activate, string expected)
    {
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/a2a")
        {
            Content = new StringContent(JsonSerializer.Serialize(new JsonRpcRequest
            {
                Id = "1",
                Method = A2AMethods.SendMessage,
                Params = JsonSerializer.SerializeToElement(new SendMessageRequest
                {
                    Message = new Message
                    {
                        MessageId = Guid.NewGuid().ToString("N"),
                        Role = Role.User,
                        Parts = [Part.FromText("tabs are best")],
                    },
                }, A2AJsonUtilities.DefaultOptions),
            }, A2AJsonUtilities.DefaultOptions), Encoding.UTF8, "application/json"),
        };
        if (activate)
        {
            request.Headers.Add("A2A-Extensions", DebateExtension.ExtensionUri);
        }

        using var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.Contains(expected, await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("""{"topic":"","argument":"tabs are best"}""")]
    [InlineData("{")]
    public async Task DebateOperation_InvalidBodyReturnsProtocolError(string json)
    {
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/a2a/debate:resume")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("A2A-Extensions", DebateExtension.ExtensionUri);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("topic and argument", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<WebApplication> StartAsync()
    {
        var app = ProtocolExtensionAgent.Program.CreateApplication(["--urls", "http://127.0.0.1:0"]);
        await app.StartAsync();
        return app;
    }

    private static HttpClient CreateClient(WebApplication app) => new()
    {
        BaseAddress = new Uri(app.Urls.Single()),
        Timeout = TimeSpan.FromSeconds(15),
    };
}
