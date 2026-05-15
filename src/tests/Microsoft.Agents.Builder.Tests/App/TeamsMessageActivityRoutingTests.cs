// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Storage;
using Moq;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Tests.App
{
    // ---------------------------------------------------------------------------
    // Teams-specific channel data and activity types used across all tests
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Example of channel-specific data for Teams activities.
    /// </summary>
    public class TeamsChannelData
    {
        public string TeamId { get; set; }
        public string ChannelId { get; set; }
    }

    /// <summary>
    /// Interface for Teams message activities with strongly typed ChannelData.
    /// </summary>
    public interface ITeamsMessageActivity : IMessageActivity
    {
        public new TeamsChannelData ChannelData { get; set; }
    }

    /// <summary>
    /// Concrete Teams message activity with typed ChannelData.
    /// </summary>
    public class TeamsMessageActivity : MessageActivity, ITeamsMessageActivity
    {
        public TeamsMessageActivity() : base()
        {
            ChannelId = Channels.Msteams;
        }

        public new TeamsChannelData ChannelData { get; set; }
    }

    /// <summary>
    /// Resolver that matches activities originating from the Teams channel.
    /// Used to deserialize message activities from Teams as <see cref="TeamsMessageActivity"/>.
    /// </summary>
    public class TeamsMessageActivityResolver : IActivityTypeResolver
    {
        public int Priority => 10;

        public bool Matches(JsonElement activityJson)
        {
            if (activityJson.TryGetProperty("channelId", out var channelId))
            {
                return string.Equals(channelId.GetString(), Channels.Msteams, System.StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }
    }

    // ---------------------------------------------------------------------------
    // Tests
    // ---------------------------------------------------------------------------

    public class TeamsMessageActivityRoutingTests
    {
        // ---------------------------------------------------------------------------
        // [MessageRoute(channelId: Channels.Msteams)] attribute routing
        // ---------------------------------------------------------------------------

        [Fact]
        public async Task MessageRouteAttribute_WithChannelId_FiresForTeamsChannel()
        {
            var app = new TeamsChannelIdApp(new AgentApplicationOptions((IStorage)null));
            var turnContext = new Mock<ITurnContext>();
            turnContext.Setup(c => c.Activity).Returns(new MessageActivity {
                ChannelId = Channels.Msteams,
                Text = "hello from Teams"
            });

            await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

            Assert.Single(app.calls);
            Assert.Equal("OnTeamsMessage", app.calls[0]);
        }

        [Fact]
        public async Task MessageRouteAttribute_WithChannelId_DoesNotFireForOtherChannel()
        {
            var app = new TeamsChannelIdApp(new AgentApplicationOptions((IStorage)null));
            var turnContext = new Mock<ITurnContext>();
            turnContext.Setup(c => c.Activity).Returns(new MessageActivity {
                ChannelId = "directline",
                Text = "hello from web"
            });

            await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

            Assert.Empty(app.calls);
        }

        [Fact]
        public async Task MessageRouteAttribute_WithChannelId_ChannelIdCaseInsensitive()
        {
            var app = new TeamsChannelIdApp(new AgentApplicationOptions((IStorage)null));
            var turnContext = new Mock<ITurnContext>();
            turnContext.Setup(c => c.Activity).Returns(new MessageActivity {
                ChannelId = "MSTEAMS",
                Text = "hello"
            });

            await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

            Assert.Single(app.calls);
            Assert.Equal("OnTeamsMessage", app.calls[0]);
        }

        // ---------------------------------------------------------------------------
        // OnActivity<ITeamsMessageActivity>() programmatic routing
        // ---------------------------------------------------------------------------

        [Fact]
        public async Task OnActivityT_FiresWhenActivityIsTeamsMessageActivity()
        {
            var app = new AgentApplicationOptions((IStorage)null);
            var calls = new List<string>();

            var agentApp = new TeamsTypedActivityApp(app, calls);

            var turnContext = new Mock<ITurnContext>();
            turnContext.Setup(c => c.Activity).Returns(new TeamsMessageActivity
            {
                ChannelData = new TeamsChannelData { TeamId = "team-123", ChannelId = "channel-abc" }
            });

            await agentApp.OnTurnAsync(turnContext.Object, CancellationToken.None);

            Assert.Single(calls);
            Assert.Equal("OnTeamsActivity", calls[0]);
        }

        [Fact]
        public async Task OnActivityT_DoesNotFireForNonTeamsMessageActivity()
        {
            var app = new AgentApplicationOptions((IStorage)null);
            var calls = new List<string>();

            var agentApp = new TeamsTypedActivityApp(app, calls);

            // A regular MessageActivity does NOT implement ITeamsMessageActivity
            var turnContext = new Mock<ITurnContext>();
            turnContext.Setup(c => c.Activity).Returns(new MessageActivity());

            await agentApp.OnTurnAsync(turnContext.Object, CancellationToken.None);

            Assert.Empty(calls);
        }

        [Fact]
        public async Task OnActivityT_TypedContext_ProvidesTeamsChannelData()
        {
            var app = new AgentApplicationOptions((IStorage)null);
            TeamsChannelData capturedChannelData = null;

            var agentApp = new TeamsTypedActivityWithCapture(app, ctx =>
            {
                capturedChannelData = ctx.Activity.ChannelData;
            });

            var expectedChannelData = new TeamsChannelData { TeamId = "team-xyz", ChannelId = "general" };
            var turnContext = new Mock<ITurnContext>();
            turnContext.Setup(c => c.Activity).Returns(new TeamsMessageActivity
            {
                ChannelData = expectedChannelData
            });

            await agentApp.OnTurnAsync(turnContext.Object, CancellationToken.None);

            Assert.NotNull(capturedChannelData);
            Assert.Equal("team-xyz", capturedChannelData.TeamId);
            Assert.Equal("general", capturedChannelData.ChannelId);
        }

        // ---------------------------------------------------------------------------
        // Resolver: deserializing message JSON with Teams channelId → TeamsMessageActivity
        // ---------------------------------------------------------------------------

        [Fact]
        public void Resolver_DeserializesToTeamsMessageActivity_WhenChannelIdIsTeams()
        {
            // Register TeamsMessageActivity as the deserialization target for
            // message activities when channelId == "msteams".
            ProtocolJsonSerializer.AddActivityResolver(
                ActivityTypes.Message,
                typeof(TeamsMessageActivity),
                new TeamsMessageActivityResolver());

            const string json = """
                {
                    "type": "message",
                    "channelId": "msteams",
                    "text": "Hello Teams"
                }
                """;

            var activity = ProtocolJsonSerializer.ToObject<Activity>(json);

            Assert.IsType<TeamsMessageActivity>(activity);
            Assert.Equal(ActivityTypes.Message, activity.Type);
        }

        [Fact]
        public void Resolver_DeserializesToMessageActivity_WhenChannelIdIsNotTeams()
        {
            // Ensure a non-Teams message still deserializes to the base MessageActivity.
            const string json = """
                {
                    "type": "message",
                    "channelId": "directline",
                    "text": "Hello Web"
                }
                """;

            var activity = ProtocolJsonSerializer.ToObject<Activity>(json);

            // The Teams resolver should not match; result is MessageActivity (the default).
            Assert.IsType<MessageActivity>(activity);
        }
    }

    // ---------------------------------------------------------------------------
    // Test agent apps
    // ---------------------------------------------------------------------------

    /// <summary>
    /// AgentApplication that routes Teams messages via [MessageRoute(channelId: Channels.Msteams)].
    /// </summary>
    class TeamsChannelIdApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public List<string> calls = [];

        [MessageRoute(channelId: Channels.Msteams)]
        public Task OnTeamsMessage(ITurnContext<IMessageActivity> ctx, ITurnState state, CancellationToken ct)
        {
            calls.Add("OnTeamsMessage");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// AgentApplication that routes via OnActivity&lt;ITeamsMessageActivity&gt;().
    /// </summary>
    class TeamsTypedActivityApp : AgentApplication
    {
        private readonly List<string> _calls;

        public TeamsTypedActivityApp(AgentApplicationOptions options, List<string> calls) : base(options)
        {
            _calls = calls;
            OnActivity<ITeamsMessageActivity>(OnTeamsActivity);
        }

        public Task OnTeamsActivity(ITurnContext<ITeamsMessageActivity> ctx, ITurnState state, CancellationToken ct)
        {
            _calls.Add("OnTeamsActivity");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// AgentApplication that captures the typed ITurnContext&lt;ITeamsMessageActivity&gt; in a callback.
    /// </summary>
    class TeamsTypedActivityWithCapture : AgentApplication
    {
        public TeamsTypedActivityWithCapture(AgentApplicationOptions options, System.Action<ITurnContext<ITeamsMessageActivity>> capture) : base(options)
        {
            OnActivity<ITeamsMessageActivity>((ctx, state, ct) =>
            {
                capture(ctx);
                return Task.CompletedTask;
            });
        }
    }
}
