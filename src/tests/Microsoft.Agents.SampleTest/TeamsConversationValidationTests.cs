// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using ConversationAgent;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.MSTeams;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.SampleTest;

public class TeamsConversationValidationTests
{
    [Fact]
    public async Task Attachments_ReportDownloadedBytesWithoutExposingLocators()
    {
        var responses = new List<IActivity>();
        using var context = new TurnContext(CreateAdapter(responses).Object, CreateActivity("files", Channels.Msteams));
        context.Activity.Attachments =
        [
            new Attachment
            {
                Name = "upload.txt",
                ContentType = "application/vnd.microsoft.teams.file.download.info",
                ContentUrl = "https://files.contoso.com/private",
                Content = new { downloadUrl = "https://files.contoso.com/bytes?token=DO-NOT-LEAK" }
            },
            new Attachment { ContentType = "text/html", Content = "<img src='DO-NOT-LEAK'>" }
        ];
        var state = new TurnState();
        state.Temp.InputFiles =
        [
            new InputFile(BinaryData.FromString("abc"), "image/png")
            {
                Filename = "image-original.png",
                ContentUrl = "https://files.contoso.com/bytes?token=DO-NOT-LEAK"
            }
        ];

        await TeamsConversationAgent.SendWelcomeCardAsync(new TeamsTurnContext(context), state, CancellationToken.None);

        var report = Assert.Single(responses);
        Assert.Equal(TextFormatTypes.Plain, report.TextFormat);
        Assert.Contains("Incoming attachments: 2", report.Text);
        Assert.Contains("Downloaded files: 1", report.Text);
        Assert.Contains("downloadUrl declared: True", report.Text);
        Assert.Contains("text/html", report.Text);
        Assert.Contains("image-original.png", report.Text);
        Assert.Contains("image/png", report.Text);
        Assert.Contains("3 bytes", report.Text);
        Assert.Contains("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD", report.Text);
        Assert.DoesNotContain("DO-NOT-LEAK", report.Text);
        Assert.DoesNotContain("https://", report.Text);
    }

    [Theory]
    [InlineData("validateconversion", "Conversation ID: PASS")]
    [InlineData("validatefinaltimeout", "Final-send fault injection is disabled")]
    [InlineData("validatelabel", "TeamsValidation:SensitivityLabelId")]
    [InlineData("validatemeeting", "No meeting metadata")]
    public async Task ValidationCommands_ReportChecks(string command, string expected)
    {
        var responses = new List<IActivity>();
        using var context = new TurnContext(CreateAdapter(responses).Object, CreateActivity(command, Channels.Msteams));
        ConfigureTeamsTransports(context);
        var agent = new TeamsConversationAgent(
            new AgentApplicationOptions(new MemoryStorage()) { LoggerFactory = NullLoggerFactory.Instance },
            NullLogger<TeamsConversationAgent>.Instance);

        await agent.OnTurnAsync(context, CancellationToken.None);

        Assert.Contains(responses, response => response.Text?.Contains(expected, StringComparison.Ordinal) == true);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FinalSendSimulation_SendsAndChecksCompleteUpdate(bool updateFails)
    {
        const string labelId = "11111111-1111-1111-1111-111111111111";
        var responses = new List<IActivity>();
        var updates = new List<IActivity>();
        var adapter = CreateAdapter(responses);
        adapter.Setup(value => value.UpdateActivityAsync(
            It.IsAny<ITurnContext>(), It.IsAny<IActivity>(), It.IsAny<CancellationToken>()))
            .Returns<ITurnContext, IActivity, CancellationToken>((_, activity, _) =>
            {
                updates.Add(activity.Clone());
                return updateFails
                    ? Task.FromException<ResourceResponse>(new InvalidOperationException("Simulated update failure."))
                    : Task.FromResult(new ResourceResponse(activity.Id));
            });
        var activity = CreateActivity("validatefinaltimeout", Channels.Msteams);
        using var context = new TurnContext(adapter.Object, activity);
        ConfigureTeamsTransports(context);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            ["TeamsValidation:EnableFinalSendFault"] = "true",
            ["TeamsValidation:SensitivityLabelId"] = labelId
        }).Build();
        using var services = new ServiceCollection().AddSingleton<IConfiguration>(configuration).BuildServiceProvider();
        var agent = ActivatorUtilities.CreateInstance<TeamsConversationAgent>(
            services, new AgentApplicationOptions(new MemoryStorage()) { LoggerFactory = NullLoggerFactory.Instance },
            NullLogger<TeamsConversationAgent>.Instance);

        await agent.OnTurnAsync(context, CancellationToken.None);

        Assert.Contains(responses, response => response.GetStreamingEntity()?.StreamType == StreamTypes.Final);
        var update = Assert.Single(updates);
        Assert.Equal("Streaming validation completed.", update.Text);
        Assert.Single(update.Attachments);
        Assert.Null(update.GetStreamingEntity());
        Assert.Equal(labelId, Assert.Single(update.Entities.OfType<AIEntity>()).UsageInfo.AtId);
        Assert.Contains(responses, response => response.Text?.Contains(
            updateFails ? "Recovery validation: FAIL" : "Recovery validation: PASS", StringComparison.Ordinal) == true);
    }

    [Theory]
    [InlineData("validateconversion")]
    [InlineData("validatemarkdown")]
    [InlineData("validatestream")]
    [InlineData("validatefinaltimeout")]
    public async Task TargetedValidation_DoesNotBroadcastReplies(string command)
    {
        var responses = new List<IActivity>();
        var activity = CreateActivity(command, Channels.Msteams);
        activity.Recipient.Properties["isTargeted"] = JsonSerializer.SerializeToElement(true);
        using var context = new TurnContext(CreateAdapter(responses).Object, activity);
        ConfigureTeamsTransports(context);
        var agent = new TeamsConversationAgent(
            new AgentApplicationOptions(new MemoryStorage()) { LoggerFactory = NullLoggerFactory.Instance },
            NullLogger<TeamsConversationAgent>.Instance);

        await agent.OnTurnAsync(context, CancellationToken.None);

        var reply = Assert.Single(responses);
        Assert.Equal(ActivityTreatmentTypes.Targeted, Assert.Single(reply.Entities.OfType<ActivityTreatment>()).Treatment);
        Assert.Equal("user-id", reply.Recipient.Id);
    }

    private static Activity CreateActivity(string text, string channel) => new()
    {
        Type = ActivityTypes.Message,
        Text = text,
        ChannelId = channel,
        ServiceUrl = "https://smba.trafficmanager.net/amer/",
        Conversation = new ConversationAccount { Id = "conversation-id" },
        From = new ChannelAccount { Id = "user-id" },
        Recipient = new ChannelAccount { Id = "agent-id" }
    };

    private static void ConfigureTeamsTransports(ITurnContext context)
    {
        var connector = new Mock<IConnectorClient>();
        connector.As<IRestTransport>().SetupGet(value => value.Endpoint).Returns(new Uri("https://smba.trafficmanager.net/amer/"));
        var tokens = new Mock<IUserTokenClient>();
        tokens.As<IRestTransport>().SetupGet(value => value.Endpoint).Returns(new Uri("https://token.botframework.com/"));
        context.Services.Set(connector.Object);
        context.Services.Set(tokens.Object);
    }

    private static Mock<IChannelAdapter> CreateAdapter(List<IActivity> responses)
    {
        var adapter = new Mock<IChannelAdapter>();
        adapter.Setup(value => value.SendActivitiesAsync(
            It.IsAny<ITurnContext>(), It.IsAny<IActivity[]>(), It.IsAny<CancellationToken>()))
            .Returns<ITurnContext, IActivity[], CancellationToken>((_, activities, _) =>
            {
                responses.AddRange(activities.Select(activity => activity.Clone()));
                return Task.FromResult(activities.Select(_ => new ResourceResponse(Guid.NewGuid().ToString())).ToArray());
            });
        return adapter;
    }
}
