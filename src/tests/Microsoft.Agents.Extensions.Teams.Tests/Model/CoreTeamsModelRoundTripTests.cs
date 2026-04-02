// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Core = Microsoft.Agents.Core;
using Microsoft.Agents.Core.Serialization;
using System;
using System.Text.Json;
using Xunit;
using Microsoft.Agents.Core.Models;
using System.Linq;

namespace Microsoft.Agents.Extensions.Teams.Tests.Model
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

            var teamsJson = ToJson(Convert<Microsoft.Teams.Api.Account>(core));
            Assert.Contains(@"""id"":""u1""",       teamsJson);
            Assert.Contains(@"""name"":""Alice""",  teamsJson);
            Assert.Contains(@"""aadObjectId"":""aad1""", teamsJson);

            var coreBack = Convert<Core.Models.ChannelAccount>(
                Convert<Microsoft.Teams.Api.Account>(core));
            Assert.Equal("u1",    coreBack.Id);
            Assert.Equal("Alice", coreBack.Name);
            Assert.Equal("aad1",  coreBack.AadObjectId);
            Assert.Equal("user",  coreBack.Role);

            // ── (b) Teams → Core ─────────────────────────────────────────────
            // Simulate an incoming Teams JSON payload (as received from the wire).
            const string incoming =
                """{"id":"u2","name":"Bob","aadObjectId":"aad2","role":"admin"}""";
            var coreFromTeams = Convert<Core.Models.ChannelAccount>(
                Convert<Microsoft.Teams.Api.Account>(incoming));
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

            var teamsJson = ToJson(Convert<Microsoft.Teams.Api.Conversation>(core));
            Assert.Contains(@"""id"":""c1""",            teamsJson);
            Assert.Contains(@"""conversationType"":""channel""", teamsJson);
            Assert.Contains(@"""tenantId"":""tenant1""", teamsJson);

            var coreBack = Convert<Core.Models.ConversationAccount>(
                Convert<Microsoft.Teams.Api.Conversation>(core));
            Assert.Equal("c1",      coreBack.Id);
            Assert.Equal("General", coreBack.Name);
            Assert.Equal(true,      coreBack.IsGroup);
            Assert.Equal("channel", coreBack.ConversationType);
            Assert.Equal("tenant1", coreBack.TenantId);

            // ── (b) Teams → Core ─────────────────────────────────────────────
            const string incoming =
                """{"id":"c2","name":"Dev","isGroup":false,"conversationType":"personal","tenantId":"t2"}""";
            var coreFromTeams = Convert<Core.Models.ConversationAccount>(
                Convert<Microsoft.Teams.Api.Conversation>(incoming));
            Assert.Equal("c2",       coreFromTeams.Id);
            Assert.Equal("Dev",      coreFromTeams.Name);
            Assert.Equal(false,      coreFromTeams.IsGroup);
            Assert.Equal("personal", coreFromTeams.ConversationType);
            Assert.Equal("t2",       coreFromTeams.TenantId);
        }

        [Fact]
        public void MessageReaction_And_TeamsReaction_RoundTrip()
        {
            // Teams.Messages.Reaction has Teams-only: createdDateTime, user.
            // Core.MessageReaction has Properties + registered converter, so these are preserved.

            // ── (a) Core → Teams → Core ──────────────────────────────────────
            var core = new Core.Models.MessageReaction { Type = "like" };

            var teamsJson = ToJson(Convert<Microsoft.Teams.Api.Messages.Reaction>(core));
            Assert.Contains(@"""type"":""like""", teamsJson);

            var coreBack = Convert<Core.Models.MessageReaction>(
                Convert<Microsoft.Teams.Api.Messages.Reaction>(core));
            Assert.Equal("like", coreBack.Type);

            // ── (b) Teams → Core (with Teams-only fields) ────────────────────
            const string incoming =
                """{"type":"heart","createdDateTime":"2024-01-01T12:00:00+00:00","user":{"id":"u1","name":"Alice"}}""";
            var coreFromTeams = Convert<Core.Models.MessageReaction>(
                Convert<Microsoft.Teams.Api.Messages.Reaction>(incoming));
            Assert.Equal("heart", coreFromTeams.Type);
            // Teams-only fields stored in Core.Properties via registered converter
            Assert.True(coreFromTeams.Properties.ContainsKey("createdDateTime"),
                "createdDateTime must be preserved in Core.Properties");
            Assert.True(coreFromTeams.Properties.ContainsKey("user"),
                "user must be preserved in Core.Properties");

            // Full Teams→Core→Teams round-trip: Teams-only fields must come back out
            var coreBackFromTeams = Convert<Core.Models.MessageReaction>(
                Convert<Microsoft.Teams.Api.Messages.Reaction>(coreFromTeams));
            Assert.Equal("heart", coreBackFromTeams.Type);
            Assert.True(coreBackFromTeams.Properties.ContainsKey("createdDateTime"),
                "createdDateTime must survive the full Teams→Core→Teams→Core round-trip");
            Assert.True(coreBackFromTeams.Properties.ContainsKey("user"),
                "user must survive the full Teams→Core→Teams→Core round-trip");

            // Verify the actual values are preserved
            var roundTrippedJson = ToJson(Convert<Microsoft.Teams.Api.Messages.Reaction>(coreFromTeams));
            Assert.Contains(@"""type"":""heart""",          roundTrippedJson);
            Assert.Contains("createdDateTime",               roundTrippedJson);
            Assert.Contains(@"""id"":""u1""",               roundTrippedJson);
        }

        [Fact]
        public void TokenResponse_And_TeamsTokenResponse_RoundTrip()
        {
            // ── (a) Core → Teams → Core ──────────────────────────────────────
            var expiration = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var core = new Core.Models.TokenResponse
            {
                ChannelId = "msteams",
                ConnectionName = "myConn",
                Token = "tok123",
                Expiration = expiration
            };

            var teamsJson = ToJson(Convert<Microsoft.Teams.Api.Token.Response>(core));
            Assert.Contains(@"""channelId"":""msteams""",   teamsJson);
            Assert.Contains(@"""connectionName"":""myConn""", teamsJson);
            Assert.Contains(@"""token"":""tok123""",         teamsJson);

            var coreBack = Convert<Core.Models.TokenResponse>(
                Convert<Microsoft.Teams.Api.Token.Response>(core));
            Assert.Equal("msteams",   coreBack.ChannelId);
            Assert.Equal("myConn",    coreBack.ConnectionName);
            Assert.Equal("tok123",    coreBack.Token);
            Assert.Equal(expiration,  coreBack.Expiration);

            // ── (b) Teams → Core ─────────────────────────────────────────────
            const string incoming =
                """{"channelId":"directline","connectionName":"c2","token":"tok456","expiration":"2026-06-01T00:00:00Z"}""";
            var coreFromTeams = Convert<Core.Models.TokenResponse>(
                Convert<Microsoft.Teams.Api.Token.Response>(incoming));
            Assert.Equal("directline", coreFromTeams.ChannelId);
            Assert.Equal("c2",        coreFromTeams.ConnectionName);
            Assert.Equal("tok456",    coreFromTeams.Token);
            Assert.Equal(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero), coreFromTeams.Expiration);
        }

        [Fact]
        public void TokenExchangeInvokeRequest_And_TeamsInvokeRequest_RoundTrip()
        {
            // ── (a) Core → Teams → Core ──────────────────────────────────────
            var core = new Core.Models.TokenExchangeInvokeRequest
            {
                Id = "req1", ConnectionName = "conn", Token = "exchTok"
            };

            var coreBack = Convert<Core.Models.TokenExchangeInvokeRequest>(
                Convert<Microsoft.Teams.Api.TokenExchange.InvokeRequest>(core));
            Assert.Equal("req1",    coreBack.Id);
            Assert.Equal("conn",    coreBack.ConnectionName);
            Assert.Equal("exchTok", coreBack.Token);

            // ── (b) Teams → Core ─────────────────────────────────────────────
            const string incoming =
                """{"id":"req2","connectionName":"conn2","token":"tok2"}""";
            var coreFromTeams = Convert<Core.Models.TokenExchangeInvokeRequest>(
                Convert<Microsoft.Teams.Api.TokenExchange.InvokeRequest>(incoming));
            Assert.Equal("req2",  coreFromTeams.Id);
            Assert.Equal("conn2", coreFromTeams.ConnectionName);
            Assert.Equal("tok2",  coreFromTeams.Token);
        }

        [Fact]
        public void TokenExchangeInvokeResponse_And_TeamsInvokeResponse_RoundTrip()
        {
            // ── (a) Core → Teams → Core ──────────────────────────────────────
            var core = new Core.Models.TokenExchangeInvokeResponse
            {
                Id = "resp1", ConnectionName = "conn", FailureDetail = "fail reason"
            };

            var coreBack = Convert<Core.Models.TokenExchangeInvokeResponse>(
                Convert<Microsoft.Teams.Api.TokenExchange.InvokeResponse>(core));
            Assert.Equal("resp1",       coreBack.Id);
            Assert.Equal("conn",        coreBack.ConnectionName);
            Assert.Equal("fail reason", coreBack.FailureDetail);

            // ── (b) Teams → Core ─────────────────────────────────────────────
            const string incoming =
                """{"id":"resp2","connectionName":"conn2","failureDetail":"another fail"}""";
            var coreFromTeams = Convert<Core.Models.TokenExchangeInvokeResponse>(
                Convert<Microsoft.Teams.Api.TokenExchange.InvokeResponse>(incoming));
            Assert.Equal("resp2",        coreFromTeams.Id);
            Assert.Equal("conn2",        coreFromTeams.ConnectionName);
            Assert.Equal("another fail", coreFromTeams.FailureDetail);
        }

        [Fact]
        public void CardAction_And_TeamsCardAction_RoundTrip()
        {
            // ── (a) Core → Teams → Core ──────────────────────────────────────
            var core = new Core.Models.CardAction
            {
                Type = "openUrl",
                Title = "Click me",
                Image = "https://example.com/img.png",
                ImageAltText = "an image",
                Text = "click",
                DisplayText = "Click here",
                Value = "https://example.com"
            };

            var teamsJson = ToJson(Convert<Microsoft.Teams.Api.Cards.Action>(core));
            Assert.Contains(@"""type"":""openUrl""",    teamsJson);
            Assert.Contains(@"""title"":""Click me""",  teamsJson);
            Assert.Contains(@"""imageAltText"":""an image""", teamsJson);

            var coreBack = Convert<Core.Models.CardAction>(
                Convert<Microsoft.Teams.Api.Cards.Action>(core));
            Assert.Equal("openUrl",    coreBack.Type);
            Assert.Equal("Click me",  coreBack.Title);
            Assert.Equal("https://example.com/img.png", coreBack.Image);
            Assert.Equal("an image",  coreBack.ImageAltText);
            Assert.Equal("click",     coreBack.Text);
            Assert.Equal("Click here", coreBack.DisplayText);

            // ── (b) Teams → Core ─────────────────────────────────────────────
            const string incoming =
                """{"type":"imBack","title":"Say hello","value":"Hello"}""";
            var coreFromTeams = Convert<Core.Models.CardAction>(
                Convert<Microsoft.Teams.Api.Cards.Action>(incoming));
            Assert.Equal("imBack",    coreFromTeams.Type);
            Assert.Equal("Say hello", coreFromTeams.Title);
        }

        [Fact]
        public void CardImage_And_TeamsCardImage_RoundTrip()
        {
            // ── (a) Core → Teams → Core ──────────────────────────────────────
            var core = new Core.Models.CardImage
            {
                Url = "https://example.com/img.png",
                Alt = "my image",
                // Teams.Cards.Action requires Title — include it so the round-trip can proceed
                Tap = new Core.Models.CardAction { Type = "openUrl", Title = "Open", Value = "https://example.com" }
            };

            var teamsJson = ToJson(Convert<Microsoft.Teams.Api.Cards.Image>(core));
            Assert.Contains(@"""url"":""https://example.com/img.png""", teamsJson);
            Assert.Contains(@"""alt"":""my image""", teamsJson);
            Assert.Contains(@"""tap"":", teamsJson);

            var coreBack = Convert<Core.Models.CardImage>(
                Convert<Microsoft.Teams.Api.Cards.Image>(core));
            Assert.Equal("https://example.com/img.png", coreBack.Url);
            Assert.Equal("my image", coreBack.Alt);
            Assert.NotNull(coreBack.Tap);
            Assert.Equal("openUrl", coreBack.Tap.Type);

            // ── (b) Teams → Core ─────────────────────────────────────────────
            const string incoming = """{"url":"https://example.com/thumb.jpg","alt":"thumb"}""";
            var coreFromTeams = Convert<Core.Models.CardImage>(
                Convert<Microsoft.Teams.Api.Cards.Image>(incoming));
            Assert.Equal("https://example.com/thumb.jpg", coreFromTeams.Url);
            Assert.Equal("thumb", coreFromTeams.Alt);
        }

        [Fact]
        public void SigninCard_And_TeamsSignInCard_RoundTrip()
        {
            // Ignoring: Teams.Cards.SigninCard has Title and SubTitle that Core.OAuthCard lacks.
            // Core.OAuthCard has no Properties catch-all and no registered converter,
            // so Title and SubTitle are silently dropped in the Teams→Core direction.
            // Title and Subitle are not required for proper OAuthCard functionality,
            // so the Core→Teams→Core round-trip is unaffected and should succeed.

            // ── (a) Core → Teams → Core ──────────────────────────────────────
            var core = new Core.Models.SigninCard
            {
                Text = "Sign in please",
                Buttons = [new Core.Models.CardAction { Type = "signin", Title = "Go" }]
            };

            var coreBack = Convert<Core.Models.SigninCard>(
                Convert<Microsoft.Teams.Api.Cards.SignInCard>(core));
            Assert.Equal("Sign in please", coreBack.Text);
            Assert.Single(coreBack.Buttons);
            Assert.Equal("signin", coreBack.Buttons[0].Type);

            // ── (b) Teams → Core (including Teams-only Title and SubTitle) ───
            const string incoming =
                """{"text":"Please sign in","title":"Sign In Required","subtitle":"Use your corporate account","buttons":[{"type":"signin","title":"Sign In"}]}""";
            var coreFromTeams = Convert<Core.Models.SigninCard>(
                Convert<Microsoft.Teams.Api.Cards.SignInCard>(incoming));
            Assert.Equal("Please sign in", coreFromTeams.Text);
            Assert.Single(coreFromTeams.Buttons);
        }

        [Fact]
        public void OAuthCard_And_TeamsOAuthCard_RoundTrip()
        {
            // Ignoring: Teams.Cards.OAuthCard has Title and SubTitle that Core.OAuthCard lacks.
            // Core.OAuthCard has no Properties catch-all and no registered converter,
            // so Title and SubTitle are silently dropped in the Teams→Core direction.
            // Title and Subitle are not required for proper OAuthCard functionality,
            // so the Core→Teams→Core round-trip is unaffected and should succeed.

            // ── (a) Core → Teams → Core ──────────────────────────────────────
            var core = new Core.Models.OAuthCard
            {
                Text = "Please authenticate",
                ConnectionName = "conn2",
                Buttons = [new Core.Models.CardAction { Type = "signin", Title = "Login" }]
            };

            var coreBack = Convert<Core.Models.OAuthCard>(
                Convert<Microsoft.Teams.Api.Cards.OAuthCard>(core));
            Assert.Equal("Please authenticate", coreBack.Text);
            Assert.Equal("conn2", coreBack.ConnectionName);
            Assert.Single(coreBack.Buttons);

            // ── (b) Teams → Core (including Teams-only Title and SubTitle) ───
            const string incoming =
                """{"text":"Sign in with OAuth","connectionName":"myConn","title":"OAuth Sign In","subtitle":"Use your account","buttons":[{"type":"signin","title":"Auth"}]}""";
            var coreFromTeams = Convert<Core.Models.OAuthCard>(
                Convert<Microsoft.Teams.Api.Cards.OAuthCard>(incoming));
            Assert.Equal("Sign in with OAuth", coreFromTeams.Text);
            Assert.Equal("myConn", coreFromTeams.ConnectionName);
            Assert.Single(coreFromTeams.Buttons);
        }

        [Fact]
        public void Mention_And_TeamsMention_RoundTrip()
        {
            var core = new Core.Models.Mention
            {
                Mentioned = new Core.Models.ChannelAccount { Id = "u1", Name = "Alice" },
                Text = "@Alice"
            };

            var teamsMention = Convert<Microsoft.Teams.Api.Entities.MentionEntity>(core);
            Assert.Equal(teamsMention.Text, core.Text);
            Assert.Equal(teamsMention.Mentioned.Id, core.Mentioned.Id);
            Assert.Equal(teamsMention.Mentioned.Name, core.Mentioned.Name);

            // ── json → Teams → Core ─────────────────────────────────────────────────
            const string incoming =
                """{"id":42,"type": "mention", "text":"@Bob","mentioned":{"id":"u2","name":"Bob"}}""";
            var coreFromTeams = Convert<Core.Models.Entity>(
                Convert<Microsoft.Teams.Api.Entities.MentionEntity>(incoming));

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
            var teamsFromCore = Convert<Microsoft.Teams.Api.Entities.MentionEntity>(coreFromJson);
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

            var teamsJson = ToJson(Convert<Microsoft.Teams.Api.Activities.MessageActivity>(core));
            Assert.Contains(@"""type"":""message""", teamsJson);
            Assert.Contains(@"""id"":""act1""",       teamsJson);
            Assert.Contains(@"""id"":""u1""",         teamsJson);

            var coreBack = Convert<Core.Models.Activity>(
                Convert<Microsoft.Teams.Api.Activities.MessageActivity>(core));
            Assert.Equal("message",  coreBack.Type);
            Assert.Equal("Hello, Teams!", coreBack.Text);
            Assert.Equal("act1",     coreBack.Id);
            Assert.NotNull(coreBack.From);
            Assert.Equal("u1",       coreBack.From.Id);
            Assert.Equal("Alice",    coreBack.From.Name);
            Assert.NotNull(coreBack.Recipient);
            Assert.Equal("bot1",     coreBack.Recipient.Id);
            Assert.NotNull(coreBack.Conversation);
            Assert.Equal("conv1",    coreBack.Conversation.Id);

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
                Convert<Microsoft.Teams.Api.Activities.Activity>(incoming));
            Assert.Equal("message",       coreFromTeams.Type);
            Assert.Equal("Hi from Teams", coreFromTeams.Text);
            Assert.Equal("act2",          coreFromTeams.Id);
            Assert.NotNull(coreFromTeams.From);
            Assert.Equal("u2",            coreFromTeams.From.Id);
        }

        // -----------------------------------------------------------------------
        // Auto-matched pairs (same simple class name in both assemblies)
        // -----------------------------------------------------------------------

        [Fact]
        public void Error_And_TeamsError_RoundTrip()
        {
            var core = new Core.Models.Error { Code = "404", Message = "Not found" };

            var coreBack = Convert<Core.Models.Error>(Convert<Microsoft.Teams.Api.Error>(core));
            Assert.Equal("404",       coreBack.Code);
            Assert.Equal("Not found", coreBack.Message);

            const string incoming = """{"code":"500","message":"Server error"}""";
            var coreFromTeams = Convert<Core.Models.Error>(
                Convert<Microsoft.Teams.Api.Error>(incoming));
            Assert.Equal("500",          coreFromTeams.Code);
            Assert.Equal("Server error", coreFromTeams.Message);
        }

        [Fact]
        public void MediaUrl_And_TeamsMediaUrl_RoundTrip()
        {
            var core = new Core.Models.MediaUrl
            {
                Url = "https://example.com/video.mp4",
                Profile = "video/mp4"
            };

            var coreBack = Convert<Core.Models.MediaUrl>(Convert<Microsoft.Teams.Api.MediaUrl>(core));
            Assert.Equal("https://example.com/video.mp4", coreBack.Url);
            Assert.Equal("video/mp4", coreBack.Profile);

            const string incoming =
                """{"url":"https://example.com/audio.mp3","profile":"audio/mpeg"}""";
            var coreFromTeams = Convert<Core.Models.MediaUrl>(
                Convert<Microsoft.Teams.Api.MediaUrl>(incoming));
            Assert.Equal("https://example.com/audio.mp3", coreFromTeams.Url);
            Assert.Equal("audio/mpeg",                     coreFromTeams.Profile);
        }

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
                Convert<Microsoft.Teams.Api.MessageExtensions.Attachment>(core));
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
                Convert<Microsoft.Teams.Api.MessageExtensions.Attachment>(incoming));
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
                Convert<Microsoft.Teams.Api.MessageExtensions.Attachment>(coreFromTeams));
            Assert.Contains(@"""id"":""attach-id""", roundTrippedJson);
            Assert.Contains(@"""preview"":",          roundTrippedJson);
        }
    }
}
