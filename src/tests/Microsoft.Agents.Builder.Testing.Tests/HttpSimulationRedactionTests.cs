// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.Testing.Http;
using System.Net;
using System.Net.Http;
using System.Text;
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
}
