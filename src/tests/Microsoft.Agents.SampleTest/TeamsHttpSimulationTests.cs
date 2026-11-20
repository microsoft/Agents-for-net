// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using ConversationAgent;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Builder.Testing.Http;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.SampleTest;

public class TeamsHttpSimulationTests
{
    private const string MemberUrl = HttpSampleHost.ServiceUrl + "v3/conversations/team-id/members/user-id";
    private const string MembersUrl = HttpSampleHost.ServiceUrl + "v3/conversations/conversation-id/pagedmembers?pageSize=100";

    [Fact]
    public async Task Targeted_PagesRosterAndSendsOnlyToReturnedRecipients()
    {
        using var http = new HttpSimulation();
        http.Expect(HttpMethod.Get, MembersUrl).RespondJson(new
        {
            continuationToken = "next page",
            members = new[] { new { id = "member-1", name = "First Member" } }
        });
        http.Expect(HttpMethod.Get, MembersUrl + "&continuationToken=next%20page").RespondJson(new
        {
            members = new[] { new { id = "member-2", name = "Second Member" } }
        });
        await using (var host = CreateHost(http))
        {
            await host.CreateTestFlow()
                .Send(HttpSampleHost.TeamsMessage("targeted"))
                .AssertReply(activity => AssertTargeted(activity, "member-1", "First Member"))
                .AssertReply(activity => AssertTargeted(activity, "member-2", "Second Member"))
                .StartTestAsync();
            Assert.Empty(host.Adapter.ActiveQueue);
        }
        http.Verify();
    }

    [Fact]
    public async Task Targeted_EmptyRosterDoesNotSendOrFetchAnotherPage()
    {
        using var http = new HttpSimulation();
        http.Expect(HttpMethod.Get, MembersUrl).RespondJson(new { members = Array.Empty<object>() });
        await using (var host = CreateHost(http))
        {
            await host.CreateTestFlow().Send(HttpSampleHost.TeamsMessage("targeted")).StartTestAsync();
            Assert.Empty(host.Adapter.ActiveQueue);
        }
        http.Verify();
    }

    [Fact]
    public async Task Mention_UsesServiceMemberIdentityInCard()
    {
        using var http = new HttpSimulation();
        http.Expect(HttpMethod.Get, MemberUrl).RespondJson(new
        {
            id = "teams-member-id",
            name = "Service Member",
            aadObjectId = "aad-member-id"
        });
        await using (var host = CreateHost(http))
        {
            await host.CreateTestFlow()
                .Send(HttpSampleHost.TeamsMessage("mentionme"))
                .AssertReply(activity =>
                {
                    var attachment = Assert.Single(activity.Attachments);
                    Assert.Equal(ContentTypes.AdaptiveCard, attachment.ContentType);
                    var card = ProtocolJsonSerializer.ToObject<JsonElement>(attachment.Content);
                    var mentions = card.GetProperty("msteams").GetProperty("entities").EnumerateArray().ToArray();
                    Assert.Equal(2, mentions.Length);
                    Assert.Equal("teams-member-id", mentions[0].GetProperty("mentioned").GetProperty("id").GetString());
                    Assert.Equal("aad-member-id", mentions[1].GetProperty("mentioned").GetProperty("id").GetString());
                    Assert.All(mentions, value =>
                        Assert.Equal("Service Member", value.GetProperty("mentioned").GetProperty("name").GetString()));
                })
                .StartTestAsync();
        }
        http.Verify();
    }

    [Fact]
    public async Task WhoAmI_UsesTeamsAndGraphServiceResponses()
    {
        using var http = new HttpSimulation();
        http.Expect(HttpMethod.Get, MemberUrl).RespondJson(new { id = "user-id", name = "Teams Member" });
        http.Expect(HttpMethod.Get, "https://graph.microsoft.com/v1.0/me")
            .WithHeader("Authorization", "Bearer synthetic-user-token")
            .RespondJson(new { id = "graph-user", displayName = "Graph Member" });
        await using (var host = CreateHost(http, signIn: true))
        {
            await host.CreateTestFlow()
                .Send(HttpSampleHost.TeamsMessage("whoami"))
                .AssertReply("You are: Teams Member.")
                .AssertReply("Graph thinks you are: Graph Member.")
                .StartTestAsync();
        }
        http.Verify();
    }

    [Theory]
    [InlineData("whoami")]
    [InlineData("mentionme")]
    public async Task MemberNotFound_ReturnsFallbackWithoutCallingGraph(string command)
    {
        using var http = new HttpSimulation();
        http.Expect(HttpMethod.Get, MemberUrl).RespondJson(
            new { error = new { code = "MemberNotFoundInConversation", message = "Synthetic missing member" } },
            HttpStatusCode.NotFound);
        await using (var host = CreateHost(http, signIn: true))
        {
            await host.CreateTestFlow()
                .Send(HttpSampleHost.TeamsMessage(command))
                .AssertReply("Member not found.")
                .StartTestAsync();
        }
        http.Verify();
    }

    [Fact]
    public async Task WhoAmI_GraphFailurePreservesTeamsReplyAndPropagatesServiceError()
    {
        using var http = new HttpSimulation();
        http.Expect(HttpMethod.Get, MemberUrl).RespondJson(new { id = "user-id", name = "Teams Member" });
        http.Expect(HttpMethod.Get, "https://graph.microsoft.com/v1.0/me")
            .RespondJson(new { error = new { code = "Authorization_RequestDenied", message = "Synthetic Graph denial" } },
                HttpStatusCode.Forbidden);
        await using (var host = CreateHost(http, signIn: true))
        {
            var error = await Assert.ThrowsAnyAsync<Microsoft.Kiota.Abstractions.ApiException>(() =>
                host.CreateTestFlow().Send(HttpSampleHost.TeamsMessage("whoami")).StartTestAsync());
            Assert.Equal(403, error.ResponseStatusCode);
            Assert.Equal("You are: Teams Member.", Assert.Single(host.Adapter.ActiveQueue).Text);
        }
        http.Verify();
    }

    [Fact]
    public async Task Targeted_NetworkFailureDoesNotSendBroadcastFallback()
    {
        using var http = new HttpSimulation();
        http.Expect(HttpMethod.Get, MembersUrl).Throw(() => new HttpRequestException("Synthetic network failure"));
        await using (var host = CreateHost(http))
        {
            await Assert.ThrowsAsync<HttpRequestException>(() =>
                host.CreateTestFlow().Send(HttpSampleHost.TeamsMessage("targeted")).StartTestAsync());
            Assert.Empty(host.Adapter.ActiveQueue);
        }
        http.Verify();
    }

    private static AgentTestHost CreateHost(HttpSimulation http, bool signIn = false) =>
        HttpSampleHost.Create(http,
            (options, _) =>
            {
                var agent = new TeamsConversationAgent(options, NullLogger<TeamsConversationAgent>.Instance);
                agent.TeamsExtension.GraphHttpMessageHandlerFactory = () => http.CreateHandler("Graph");
                return agent;
            },
            Channels.Msteams, signIn);

    private static void AssertTargeted(IActivity activity, string recipient, string name)
    {
        Assert.Contains(name, activity.Text);
        Assert.Equal(recipient, activity.Recipient.Id);
        Assert.Equal(ActivityTreatmentTypes.Targeted,
            Assert.Single(activity.Entities.OfType<ActivityTreatment>()).Treatment);
    }
}
