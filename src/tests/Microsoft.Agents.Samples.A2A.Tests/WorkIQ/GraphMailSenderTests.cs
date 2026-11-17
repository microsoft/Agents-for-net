extern alias WorkIQBriefingSample;

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WorkIQBriefingSample::WorkIQBriefingAgent;

namespace Microsoft.Agents.Samples.A2A.Tests;

public class GraphMailSenderTests
{
    [Fact]
    public async Task Send_PostsTheApprovedEmailWithADelegatedTokenAndSentItemsCopy()
    {
        using var http = new SampleHttpClients((request, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)));
        var sender = new GraphMailSender(http);

        var result = await sender.SendAsync(
            new BriefingMail(["reviewer@example.com"], "Contoso briefing", "Approved body"),
            "delegated-graph-token", CancellationToken.None);

        Assert.Equal(MailDeliveryState.Accepted, result);
        var sent = Assert.Single(http.Requests);
        Assert.Equal(HttpMethod.Post, sent.Method);
        Assert.Equal("https://graph.microsoft.com/v1.0/me/sendMail", sent.Uri.AbsoluteUri);
        Assert.Equal("delegated-graph-token", sent.Token);
        using var body = JsonDocument.Parse(sent.Body);
        var message = body.RootElement.GetProperty("message");
        Assert.Equal("Contoso briefing", message.GetProperty("subject").GetString());
        Assert.Equal("Text", message.GetProperty("body").GetProperty("contentType").GetString());
        Assert.Equal("Approved body", message.GetProperty("body").GetProperty("content").GetString());
        Assert.Equal("reviewer@example.com", message.GetProperty("toRecipients")[0].GetProperty("emailAddress").GetProperty("address").GetString());
        Assert.True(body.RootElement.GetProperty("saveToSentItems").GetBoolean());
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, (int)MailDeliveryState.Rejected)]
    [InlineData(HttpStatusCode.Unauthorized, (int)MailDeliveryState.Rejected)]
    [InlineData(HttpStatusCode.TooManyRequests, (int)MailDeliveryState.Rejected)]
    [InlineData(HttpStatusCode.InternalServerError, (int)MailDeliveryState.Unknown)]
    [InlineData(HttpStatusCode.RequestTimeout, (int)MailDeliveryState.Unknown)]
    [InlineData(HttpStatusCode.OK, (int)MailDeliveryState.Unknown)]
    public async Task UnexpectedResponses_AreNotReportedAsAcceptedOrRetried(HttpStatusCode status, int expected)
    {
        using var http = new SampleHttpClients((request, ct) => Task.FromResult(new HttpResponseMessage(status)));
        var sender = new GraphMailSender(http);

        var result = await sender.SendAsync(new BriefingMail(["test@example.com"], "Subject", "Body"), "token", CancellationToken.None);

        Assert.Equal((MailDeliveryState)expected, result);
        Assert.Single(http.Requests);
    }

    [Fact]
    public async Task SeparateSends_DoNotShareTheOtherUsersToken()
    {
        using var http = new SampleHttpClients((request, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)));
        var sender = new GraphMailSender(http);
        await Task.WhenAll(
            sender.SendAsync(new BriefingMail(["a@example.com"], "A", "A"), "token-a", CancellationToken.None),
            sender.SendAsync(new BriefingMail(["b@example.com"], "B", "B"), "token-b", CancellationToken.None));

        Assert.Equal(["token-a", "token-b"], http.Requests.Select(request => request.Token!).OrderBy(value => value).ToArray());
    }
}
