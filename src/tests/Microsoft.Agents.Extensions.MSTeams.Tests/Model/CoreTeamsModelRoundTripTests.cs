// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Core = Microsoft.Agents.Core;
using Microsoft.Agents.Core.Serialization;
using System;
using System.Text.Json;
using Xunit;
using Microsoft.Agents.Core.Models;
using System.Linq;

namespace Microsoft.Agents.Extensions.MSTeams.Tests.Model
{
    /// <summary>
    /// Proves that every paired Core↔Teams model type can be serialized to JSON from one side and
    /// deserialized on the other side without losing any property, and that the trip is fully
    /// reversible.
    ///
    /// Each test exercises both directions:
    ///   (a) Core → Teams → Core: properties built in C# on the Core side survive the round-trip.
    ///   (b) Teams → Core: properties from an incoming Teams JSON payload land correctly in Core.
    ///       Teams-only properties that the Core model can preserve (via <c>Properties</c> + a
    ///       registered <see cref="JsonConverter"/>) are also verified for full preservation.
    ///
    /// A failing test indicates data is silently dropped during the conversion — a bug to fix.
    ///
    /// Note: Teams.Api uses value-type wrappers (Role, ContentType, ActionType, ChannelId…)
    /// that have no implicit conversion from <see langword="string"/>.  To keep the tests
    /// free from Teams-specific strong-type construction, Teams model instances are constructed
    /// from raw JSON strings (matching real wire payloads) and assertions are made exclusively
    /// against Core model properties or intermediate JSON output.
    /// </summary>
    public class CoreTeamsModelRoundTripTests
    {
        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        /// <summary>Serialize <paramref name="source"/> to JSON then deserialize as <typeparamref name="T"/>.</summary>
        private static T Convert<T>(object source) =>
            ProtocolJsonSerializer.ToObject<T>(source);

        /// <summary>Serialize <paramref name="value"/> to a JSON string.</summary>
        private static string ToJson(object value) =>
            ProtocolJsonSerializer.ToJson(value);

        // -----------------------------------------------------------------------
        // Explicit mappings
        // -----------------------------------------------------------------------

        [Fact]
        public void ChannelAccount_And_TeamsAccount_RoundTrip()
        {
            // ── (a) Core → Teams → Core ──────────────────────────────────────
            var core = new Core.Models.ChannelAccount
            {
                Id = "u1", Name = "Alice", AadObjectId = "aad1", Role = "user"
            };

            var teamsJson = ToJson(Convert<Microsoft.Teams.Apps.Schema.TeamsChannelAccount>(core));
            Assert.Contains(@"""id"":""u1""",       teamsJson);
            Assert.Contains(@"""name"":""Alice""",  teamsJson);
            Assert.Contains(@"""aadObjectId"":""aad1""", teamsJson);

            var coreBack = Convert<Core.Models.ChannelAccount>(
                Convert<Microsoft.Teams.Apps.Schema.TeamsChannelAccount>(core));
            Assert.Equal("u1",    coreBack.Id);
            Assert.Equal("Alice", coreBack.Name);
            Assert.Equal("aad1",  coreBack.AadObjectId);
            Assert.Equal("user",  coreBack.Role);

            // ── (b) Teams → Core ─────────────────────────────────────────────
            // Simulate an incoming Teams JSON payload (as received from the wire).
            const string incoming =
                """{"id":"u2","name":"Bob","aadObjectId":"aad2","role":"admin"}""";
            var coreFromTeams = Convert<Core.Models.ChannelAccount>(
                Convert<Microsoft.Teams.Apps.Schema.TeamsChannelAccount>(incoming));
            Assert.Equal("u2",    coreFromTeams.Id);
            Assert.Equal("Bob",   coreFromTeams.Name);
            Assert.Equal("aad2",  coreFromTeams.AadObjectId);
            Assert.Equal("admin", coreFromTeams.Role);
        }

        [Fact]
        public void ConversationAccount_And_TeamsConversation_RoundTrip()
        {
            // Teams.Conversation.Type (C#) maps to json:"conversationType"
            // Core.ConversationAccount.ConversationType maps to the same json name.

            // ── (a) Core → Teams → Core ──────────────────────────────────────
            var core = new Core.Models.ConversationAccount
            {
                Id = "c1", Name = "General", IsGroup = true,
                ConversationType = "channel", TenantId = "tenant1"
            };

            var teamsJson = ToJson(Convert<Microsoft.Teams.Apps.Schema.TeamsConversation>(core));
            Assert.Contains(@"""id"":""c1""",            teamsJson);
            Assert.Contains(@"""conversationType"":""channel""", teamsJson);
            Assert.Contains(@"""tenantId"":""tenant1""", teamsJson);

            var coreBack = Convert<Core.Models.ConversationAccount>(
                Convert<Microsoft.Teams.Apps.Schema.TeamsConversation>(core));
            Assert.Equal("c1",      coreBack.Id);
            Assert.Equal("General", coreBack.Name);
            Assert.Equal(true,      coreBack.IsGroup);
            Assert.Equal("channel", coreBack.ConversationType);
            Assert.Equal("tenant1", coreBack.TenantId);

            // ── (b) Teams → Core ─────────────────────────────────────────────
            const string incoming =
                """{"id":"c2","name":"Dev","isGroup":false,"conversationType":"personal","tenantId":"t2"}""";
            var coreFromTeams = Convert<Core.Models.ConversationAccount>(
                Convert<Microsoft.Teams.Apps.Schema.TeamsConversation>(incoming));
            Assert.Equal("c2",       coreFromTeams.Id);
            Assert.Equal("Dev",      coreFromTeams.Name);
            Assert.Equal(false,      coreFromTeams.IsGroup);
            Assert.Equal("personal", coreFromTeams.ConversationType);
            Assert.Equal("t2",       coreFromTeams.TenantId);
        }

        [Fact]
#pragma warning disable ExperimentalTeamsReactions // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        public void MessageReaction_And_TeamsReaction_RoundTrip()
        {
            // ── (a) Core → Teams → Core ──────────────────────────────────────
            var core = new Core.Models.MessageReaction { Type = "like" };

            var teamsJson = ToJson(Convert<Microsoft.Teams.Apps.MessageReaction>(core));
            Assert.Contains(@"""type"":""like""", teamsJson);

            var coreBack = Convert<Core.Models.MessageReaction>(
                Convert<Microsoft.Teams.Apps.MessageReaction>(core));
            Assert.Equal("like", coreBack.Type);

            // ── (b) Teams → Core ─────────────────────────────────────────────
            const string incoming =
                """{"type":"heart","createdDateTime":"2024-01-01T12:00:00+00:00","user":{"id":"u1","name":"Alice"}}""";
            var coreFromTeams = Convert<Core.Models.MessageReaction>(
                Convert<Microsoft.Teams.Apps.MessageReaction>(incoming));
            Assert.Equal("heart", coreFromTeams.Type);

            var coreBackFromTeams = Convert<Core.Models.MessageReaction>(
                Convert<Microsoft.Teams.Apps.MessageReaction>(coreFromTeams));
            Assert.Equal("heart", coreBackFromTeams.Type);
        }
#pragma warning restore ExperimentalTeamsReactions // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

        [Fact]
        public void Mention_And_TeamsMention_RoundTrip()
        {
            var core = new Core.Models.Mention
            {
                Mentioned = new Core.Models.ChannelAccount { Id = "u1", Name = "Alice" },
                Text = "@Alice"
            };

            var teamsMention = Convert<Microsoft.Teams.Apps.Schema.Entities.MentionEntity>(core);
            Assert.Equal(teamsMention.Text, core.Text);
            Assert.Equal(teamsMention.Mentioned.Id, core.Mentioned.Id);
            Assert.Equal(teamsMention.Mentioned.Name, core.Mentioned.Name);

            // ── json → Teams → Core ─────────────────────────────────────────────────
            const string incoming =
                """{"id":42,"type": "mention", "text":"@Bob","mentioned":{"id":"u2","name":"Bob"}}""";
            var coreFromTeams = Convert<Core.Models.Entity>(
                Convert<Microsoft.Teams.Apps.Schema.Entities.MentionEntity>(incoming));

            Assert.IsType<Mention>(coreFromTeams);
            var mention = coreFromTeams as Mention;
            Assert.NotNull(mention.Mentioned);
            Assert.Equal("u2", mention.Mentioned.Id);
            Assert.Equal("Bob", mention.Mentioned.Name);
            Assert.True(mention.Properties.ContainsKey("id"));

            var coreJson = ToJson(coreFromTeams);
            Assert.Contains(@"""id"":42", coreJson);

            // ── json → Core → Teams ─────────────────────────────────────────────────
            var coreFromJson = Convert<Core.Models.Entity>(incoming);
            Assert.IsType<Mention>(coreFromJson);
            var teamsFromCore = Convert<Microsoft.Teams.Apps.Schema.Entities.MentionEntity>(coreFromJson);
            Assert.NotNull(teamsFromCore.Mentioned);
            Assert.Equal("u2", teamsFromCore.Mentioned.Id);
            Assert.Equal("Bob", teamsFromCore.Mentioned.Name);
            Assert.True(teamsFromCore.Properties.ContainsKey("id"));
        }

        // -----------------------------------------------------------------------
        // Activity (most common type)
        // -----------------------------------------------------------------------

        [Fact]
        public void Activity_And_TeamsMessageActivity_RoundTrip()
        {
            // ── (a) Core → Teams → Core ──────────────────────────────────────
            var core = new Core.Models.Activity
            {
                Type = "message",
                Text = "Hello, Teams!",
                From = new Core.Models.ChannelAccount { Id = "u1", Name = "Alice" },
                Recipient = new Core.Models.ChannelAccount { Id = "bot1", Name = "MyBot" },
                Conversation = new Core.Models.ConversationAccount { Id = "conv1" },
                Id = "act1"
            };

            var teamsJson = ToJson(Convert<Microsoft.Teams.Apps.MessageActivity>(core));
            Assert.Contains(@"""type"":""message""", teamsJson);
            Assert.Contains(@"""id"":""act1""",       teamsJson);

            var coreBack = Convert<Core.Models.Activity>(
                Convert<Microsoft.Teams.Apps.MessageActivity>(core));
            Assert.Equal("message",  coreBack.Type);
            Assert.Equal("Hello, Teams!", coreBack.Text);
            Assert.Equal("act1",     coreBack.Id);

            // ── (b) Teams → Core ─────────────────────────────────────────────
            const string incoming = """
                {
                  "type": "message",
                  "id": "act2",
                  "text": "Hi from Teams",
                  "from": {"id": "u2", "name": "Bob"},
                  "recipient": {"id": "bot2", "name": "Agent"},
                  "conversation": {"id": "conv2"}
                }
                """;
            var coreFromTeams = Convert<Core.Models.Activity>(
                Convert<Microsoft.Teams.Apps.Schema.TeamsActivity>(incoming));
            Assert.Equal("message",       coreFromTeams.Type);
            Assert.Equal("Hi from Teams", coreFromTeams.Text);
            Assert.Equal("act2",          coreFromTeams.Id);
        }

        // -----------------------------------------------------------------------
        // Auto-matched pairs (same simple class name in both assemblies)
        // -----------------------------------------------------------------------

        [Fact]
        public void Attachment_And_TeamsAttachment_RoundTrip()
        {
            // Core.Attachment has Properties + registered converter.
            // Teams.MessageExtensions.Attachment adds Teams-only "id" and "preview"
            // which Core preserves in Properties and writes back on serialization.

            // ── (a) Core → Teams → Core ──────────────────────────────────────
            var core = new Core.Models.Attachment
            {
                ContentType = "application/vnd.microsoft.card.hero",
                ContentUrl  = "https://example.com/content",
                Name        = "my-attachment"
            };

            var coreBack = Convert<Core.Models.Attachment>(
                Convert<Microsoft.Teams.Apps.Schema.TeamsAttachment>(core));
            Assert.Equal("application/vnd.microsoft.card.hero", coreBack.ContentType);
            Assert.Equal("https://example.com/content",          coreBack.ContentUrl);
            Assert.Equal("my-attachment",                         coreBack.Name);

            // ── (b) Teams → Core (including Teams-only id and preview) ───────
            const string incoming = """
                {
                  "contentType": "text/plain",
                  "contentUrl":  "https://example.com/doc.txt",
                  "name":        "doc.txt",
                  "id":          "attach-id",
                  "preview":     {"contentType": "text/plain", "name": "preview"}
                }
                """;
            var coreFromTeams = Convert<Core.Models.Attachment>(
                Convert<Microsoft.Teams.Apps.Schema.TeamsAttachment>(incoming));
            Assert.Equal("text/plain",                  coreFromTeams.ContentType);
            Assert.Equal("https://example.com/doc.txt", coreFromTeams.ContentUrl);
            Assert.Equal("doc.txt",                     coreFromTeams.Name);
            // Teams-only fields must land in Core.Properties
            Assert.True(coreFromTeams.Properties.ContainsKey("id"),
                "Teams-only 'id' must be preserved in Core.Properties");
            Assert.True(coreFromTeams.Properties.ContainsKey("preview"),
                "Teams-only 'preview' must be preserved in Core.Properties");

            // Full Teams→Core→Teams round-trip: id and preview must be restored
            var roundTrippedJson = ToJson(
                Convert<Microsoft.Teams.Apps.Schema.TeamsAttachment>(coreFromTeams));
            Assert.Contains(@"""id"":""attach-id""", roundTrippedJson);
            Assert.Contains(@"""preview"":",          roundTrippedJson);
        }
    }
}
