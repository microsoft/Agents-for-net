using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Agents.ApiCompat;
using Xunit;

namespace Microsoft.Agents.ApiCompat.Tests;

public sealed class BaselineResolverTests
{
    [Fact]
    public void SelectBaseline_Main_ReturnsLatestStable()
    {
        var versions = new[] { "1.2.0", "2.0.0-beta.1", "1.9.0", "2.0.0" };

        Assert.Equal("2.0.0", NuGetBaselineResolver.SelectBaseline("main", versions));
    }

    [Fact]
    public void SelectBaseline_ReleaseBranch_StaysInMajorMinorLine()
    {
        var versions = new[] { "1.7.9", "1.7.123", "1.8.0", "1.7.129-beta.1" };

        Assert.Equal("1.7.123", NuGetBaselineResolver.SelectBaseline("rel/v1.7", versions));
    }

    [Fact]
    public async Task GetBaselineVersionAsync_ReturnsNullWhenFeedIsMissing()
    {
        using var client = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));
        var resolver = new NuGetBaselineResolver(client);

        var result = await resolver.GetBaselineVersionAsync("Microsoft.Agents.Core", "main", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetBaselineVersionAsync_ReturnsLatestStableFromFeed()
    {
        HttpRequestMessage? capturedRequest = null;
        using var client = new HttpClient(new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return JsonResponse("""
                {
                  "versions": [ "1.7.9", "1.7.123", "1.8.0-beta.1" ]
                }
                """);
        }));
        var resolver = new NuGetBaselineResolver(client);

        var result = await resolver.GetBaselineVersionAsync("Microsoft.Agents.Core", "rel/v1.7", CancellationToken.None);

        Assert.Equal("1.7.123", result);
        Assert.Equal(
            "https://api.nuget.org/v3-flatcontainer/microsoft.agents.core/index.json",
            capturedRequest?.RequestUri?.ToString());
    }

    [Fact]
    public async Task DownloadAsync_WritesPackageBytesToDestination()
    {
        HttpRequestMessage? capturedRequest = null;
        using var client = new HttpClient(new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent("nupkg-bytes"u8.ToArray()),
            };
        }));
        var resolver = new NuGetBaselineResolver(client);
        var destination = Path.Combine(AppContext.BaseDirectory, $"package-{Guid.NewGuid():N}.nupkg");

        try
        {
            await resolver.DownloadAsync("Microsoft.Agents.Core", "1.7.123", destination, CancellationToken.None);

            Assert.Equal("nupkg-bytes", File.ReadAllText(destination));
            Assert.Equal(
                "https://api.nuget.org/v3-flatcontainer/microsoft.agents.core/1.7.123/microsoft.agents.core.1.7.123.nupkg",
                capturedRequest?.RequestUri?.ToString());
        }
        finally
        {
            if (File.Exists(destination))
            {
                File.Delete(destination);
            }
        }
    }

    private static HttpResponseMessage JsonResponse(string content)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json"),
        };
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }
}
