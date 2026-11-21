// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using HandlingAttachments;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Builder.Testing.Http;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.SampleTest;

public class AttachmentsHttpSimulationTests
{
    private const string UploadUrl = "https://channel.example.test/v3/conversations/conversation-id/attachments";
    private const string DownloadUrl = "https://files.example.test/upload.png";

    [Fact]
    public async Task UploadSendsPngBytesThroughConnectorAndUsesReturnedAttachmentUri()
    {
        var image = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Resources", "agents-sdk.png"));
        using var http = new HttpSimulation(new HttpSimulationOptions { MaxRequestBodyBytes = 256 * 1024 });
        http.Expect(HttpMethod.Post, UploadUrl)
            .WithHeader("Authorization", "Bearer synthetic-agent-token")
            .WithJsonBody(body =>
                body.GetProperty("name").GetString() == @"Resources\agents-sdk.png" &&
                body.GetProperty("type").GetString() == "image/png" &&
                body.GetProperty("originalBase64").GetString() == Convert.ToBase64String(image))
            .RespondJson(new { id = "uploaded-image" }, HttpStatusCode.Created);

        await using (var host = CreateHost(http))
        {
            await host.CreateTestFlow()
                .Send("3")
                .AssertReplySatisfies(reply =>
                {
                    Assert.Equal("This is an uploaded attachment.", reply.Text);
                    var attachment = Assert.Single(reply.Attachments);
                    Assert.Equal("https://channel.example.test/v3/attachments/uploaded-image/views/original", attachment.ContentUrl);
                    Assert.Equal("image/png", attachment.ContentType);
                    return Task.CompletedTask;
                })
                .AssertReplySatisfies(AssertOptions)
                .StartTestAsync();
            Assert.Null(host.Adapter.GetNextReply());
        }

        http.Verify();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UploadFailureDoesNotSendAnAttachmentReply(bool networkFault)
    {
        using var http = new HttpSimulation();
        var upload = http.Expect(HttpMethod.Post, UploadUrl);
        if (networkFault)
        {
            upload.Throw(() => new HttpRequestException("Upload transport unavailable"));
        }
        else
        {
            upload.RespondJson(new { error = new { code = "Forbidden", message = "Upload forbidden" } }, HttpStatusCode.Forbidden);
        }

        await using (var host = CreateHost(http))
        {
            var failure = await Record.ExceptionAsync(() => host.CreateTestFlow().Send("3").StartTestAsync());
            Assert.NotNull(failure);
            if (networkFault)
            {
                Assert.IsType<HttpRequestException>(failure);
            }
            else
            {
                var responseFailure = Assert.IsType<ErrorResponseException>(failure);
                Assert.Equal(403, responseFailure.StatusCode);
                Assert.Equal("Forbidden", responseFailure.Body.Error.Code);
            }
            Assert.Null(host.Adapter.GetNextReply());
        }

        http.Verify();
    }

    [Fact]
    public async Task FactoryBackedInputDownloadIsEchoedAsAnInlineAttachment()
    {
        var image = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Resources", "build-agents.png"));
        using var http = new HttpSimulation();
        http.Expect(HttpMethod.Get, DownloadUrl)
            .ForClient(nameof(AttachmentDownloader))
            .Respond(HttpStatusCode.OK, response =>
            {
                response.Content = new ByteArrayContent(image);
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            });

        await using (var host = CreateHost(http, download: true))
        {
            await host.CreateTestFlow()
                .Send(IncomingImage())
                .AssertReplySatisfies(reply =>
                {
                    Assert.Equal("There are 1 attachments.", reply.Text);
                    var attachment = Assert.Single(reply.Attachments);
                    Assert.Equal("upload.png", attachment.Name);
                    Assert.Equal("image/png", attachment.ContentType);
                    Assert.Equal("data:image/png;base64," + Convert.ToBase64String(image), attachment.ContentUrl);
                    return Task.CompletedTask;
                })
                .AssertReplySatisfies(AssertOptions)
                .StartTestAsync();
        }

        http.Verify();
    }

    [Fact]
    public async Task DownloadNonSuccessShowsOptionsWithoutEchoingUnavailableContent()
    {
        using var http = new HttpSimulation();
        http.Expect(HttpMethod.Get, DownloadUrl).Respond(HttpStatusCode.NotFound);

        await using (var host = CreateHost(http, download: true))
        {
            await host.CreateTestFlow().Send(IncomingImage()).AssertReplySatisfies(AssertOptions).StartTestAsync();
            Assert.Null(host.Adapter.GetNextReply());
        }

        http.Verify();
    }

    [Fact]
    public async Task DownloadNetworkFailurePropagatesWithoutSendingPartialAttachment()
    {
        using var http = new HttpSimulation();
        http.Expect(HttpMethod.Get, DownloadUrl).Throw(() => new HttpRequestException("Download transport unavailable"));

        await using (var host = CreateHost(http, download: true))
        {
            await Assert.ThrowsAsync<HttpRequestException>(() => host.CreateTestFlow().Send(IncomingImage()).StartTestAsync());
            Assert.Null(host.Adapter.GetNextReply());
        }

        http.Verify();
    }

    private static AgentTestHost CreateHost(HttpSimulation http, bool download = false) =>
        HttpSampleHost.Create(http, (options, _) =>
        {
            if (download)
            {
                // This is the sample's general factory-backed path, not its separately validated M365 transport.
                options.FileDownloaders = [new AttachmentDownloader(options.HttpClientFactory)];
            }
            return new AttachmentsAgent(options);
        });

    private static Activity IncomingImage() => new()
    {
        Type = ActivityTypes.Message,
        Attachments = [new Attachment { Name = "upload.png", ContentType = "image/png", ContentUrl = DownloadUrl }]
    };

    private static Task AssertOptions(IActivity reply)
    {
        Assert.Equal(HeroCard.ContentType, Assert.Single(reply.Attachments).ContentType);
        return Task.CompletedTask;
    }
}
