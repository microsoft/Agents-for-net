// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App.Proactive;
using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Xunit;

namespace Microsoft.Agents.Builder.Tests.App
{
    public class ConversationBuilderTests
    {
        [Fact]
        public void Create_ShouldReturnNewInstance()
        {
            // Act
            var builder = ConversationBuilder.Create();

            // Assert
            Assert.NotNull(builder);
            Assert.IsType<ConversationBuilder>(builder);
        }

        [Fact]
        public void WithReference_ReferenceBuilder_ShouldSetTeamsReferenceBuilder()
        {
            // Arrange
            var builder = ConversationBuilder.Create();
            var refBuilder = ConversationReferenceBuilder.Create(Channels.Msteams, "conv123")
                .WithUser("user123")
                .WithAgent("agent123")
                .WithServiceUrl("https://test.com");

            var claims = new Dictionary<string, string>
            {
                { "aud", "testAudience" }
            };

            // Act
            var result = builder
                .WithReference(refBuilder.Build())
                .WithClaims(claims)
                .Build();

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Reference);
            Assert.Equal("conv123", result.Reference.Conversation.Id);
            Assert.Equal("user123", result.Reference.User.Id);
            Assert.Equal("28:agent123", result.Reference.Agent.Id);
        }

        [Fact]
        public void WithReference_ConversationReference_ShouldSetReference()
        {
            // Arrange
            var builder = ConversationBuilder.Create();
            var reference = new ConversationReference
            {
                ChannelId = Channels.Msteams,
                Conversation = new ConversationAccount { Id = "conv123" },
                User = new ChannelAccount { Id = "user123" },
                Agent = new ChannelAccount { Id = "agent123" },
                ServiceUrl = "https://test.com"
            };

            var claims = new Dictionary<string, string>
            {
                { "aud", "testAudience" }
            };

            // Act
            var result = builder
                .WithReference(reference)
                .WithClaims(claims)
                .Build();

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Reference);
            Assert.Equal(reference.Conversation.Id, result.Reference.Conversation.Id);
            Assert.Equal(reference.User.Id, result.Reference.User.Id);
            Assert.Equal(reference.Agent.Id, result.Reference.Agent.Id);
        }

        [Fact]
        public void WithClaimsFromClientId_WithClientIdOnly_ShouldSetClaims()
        {
            // Arrange
            var builder = ConversationBuilder.Create();
            var clientId = "client123";
            var reference = new ConversationReference
            {
                ChannelId = Channels.Msteams,
                Conversation = new ConversationAccount { Id = "conv123" }
            };

            // Act
            var result = builder
                .WithReference(reference)
                .WithClaimsForClientId(clientId)
                .Build();

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsValid());
        }

        [Fact]
        public void WithClaimsFromClientId_WithRequestorId_ShouldSetBothClaims()
        {
            // Arrange
            var builder = ConversationBuilder.Create();
            var clientId = "client123";
            var requestorId = "requestor456";
            var reference = new ConversationReference
            {
                ChannelId = Channels.Msteams,
                Conversation = new ConversationAccount { Id = "conv123" }
            };

            // Act
            var result = builder
                .WithReference(reference)
                .WithClaimsForClientId(clientId, requestorId)
                .Build();

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsValid());
            Assert.Equal(requestorId, result.Identity.Claims.First(c => c.Type == "appid")?.Value);
        }

        [Fact]
        public void WithClaimsFromClientId_WithEmptyRequestorId_ShouldOnlySetAudClaim()
        {
            // Arrange
            var builder = ConversationBuilder.Create();
            var clientId = "client123";
            var reference = new ConversationReference
            {
                ChannelId = Channels.Msteams,
                Conversation = new ConversationAccount { Id = "conv123" }
            };

            // Act
            var result = builder
                .WithReference(reference)
                .WithClaimsForClientId(clientId, string.Empty)
                .Build();

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsValid());
            Assert.Null(result.Identity.Claims.FirstOrDefault(c => c.Type == "appid"));
        }

        [Fact]
        public void WithClaims_ShouldSetClaims()
        {
            // Arrange
            var builder = ConversationBuilder.Create();
            var claims = new Dictionary<string, string>
            {
                { "aud", "audience123" },
                { "appid", "app456" },
                { "iss", "issuer789" }
            };
            var reference = new ConversationReference
            {
                ChannelId = Channels.Msteams,
                Conversation = new ConversationAccount { Id = "conv123" }
            };

            // Act
            var result = builder
                .WithReference(reference)
                .WithClaims(claims)
                .Build();

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsValid());
            Assert.Equal("app456", result.Identity.Claims.First(c => c.Type == "appid").Value);
            Assert.Equal("issuer789", result.Identity.Claims.First(c => c.Type == "iss").Value);
        }

        [Fact]
        public void WithClaims_NullClaims_ShouldThrow()
        {
            // Arrange
            var builder = ConversationBuilder.Create();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => builder.WithClaims(null));
        }

        [Fact]
        public void WithClaims_EmptyClaims_ShouldThrow()
        {
            // Arrange
            var builder = ConversationBuilder.Create();
            var claims = new Dictionary<string, string>();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => builder.WithClaims(claims));
        }

        [Fact]
        public void WithIdentity_ShouldExtractClaimsFromIdentity()
        {
            // Arrange
            var builder = ConversationBuilder.Create();
            var identity = new ClaimsIdentity(new[]
            {
                new Claim("aud", "audience123"),
                new Claim("azp", "azp456"),
                new Claim("appid", "app789"),
                new Claim("idtyp", "app"),
                new Claim("ver", "1.0"),
                new Claim("iss", "issuer111"),
                new Claim("other", "shouldBeIgnored")
            });
            var reference = new ConversationReference
            {
                ChannelId = Channels.Msteams,
                Conversation = new ConversationAccount { Id = "conv123" }
            };

            // Act
            var result = builder
                .WithReference(reference)
                .WithIdentity(identity)
                .Build();

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsValid());
            Assert.Equal("azp456", result.Identity.Claims.First(c => c.Type == "azp").Value);
            Assert.Equal("app789", result.Identity.Claims.First(c => c.Type == "appid").Value);
            Assert.Equal("app", result.Identity.Claims.First(c => c.Type == "idtyp").Value);
            Assert.Equal("1.0", result.Identity.Claims.First(c => c.Type == "ver").Value);
            Assert.Equal("issuer111", result.Identity.Claims.First(c => c.Type == "iss").Value);
            Assert.Null(result.Identity.Claims.FirstOrDefault(c => c.Type == "other"));
        }

        [Fact]
        public void Build_WithoutClaims_ShouldThrow()
        {
            // Arrange
            var builder = ConversationBuilder.Create();
            var reference = new ConversationReference
            {
                ChannelId = Channels.Msteams,
                Conversation = new ConversationAccount { Id = "conv123" }
            };

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                builder.WithReference(reference).Build());
        }

        [Fact]
        public void Build_WithoutReference_ShouldThrow()
        {
            // Arrange
            var builder = ConversationBuilder.Create();
            var claims = new Dictionary<string, string>
            {
                { "aud", "audience123" }
            };

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                builder.WithClaims(claims).Build());
        }

        [Fact]
        public void Build_WithReferenceBuilder_ShouldBuildTeamsReferenceFromBuilder()
        {
            // Arrange
            var builder = ConversationBuilder.Create();
            var refBuilder = ConversationReferenceBuilder.Create(Channels.Msteams, "conv123")
                .WithUser("user123", "User Name")
                .WithAgent("agent123", "Agent Name")
                .WithServiceUrl("https://test.com")
                .WithActivityId("activity123")
                .WithLocale("en-US");

            var claims = new Dictionary<string, string>
            {
                { "aud", "audience123" }
            };

            // Act
            var result = builder
                .WithReference(refBuilder.Build())
                .WithClaims(claims)
                .Build();

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Reference);
            Assert.Equal(Channels.Msteams, result.Reference.ChannelId);
            Assert.Equal("conv123", result.Reference.Conversation.Id);
            Assert.Equal("user123", result.Reference.User.Id);
            Assert.Equal("User Name", result.Reference.User.Name);
            Assert.Equal("28:agent123", result.Reference.Agent.Id);
            Assert.Equal("Agent Name", result.Reference.Agent.Name);
            Assert.Equal("https://test.com", result.Reference.ServiceUrl);
            Assert.Equal("activity123", result.Reference.ActivityId);
            Assert.Equal("en-US", result.Reference.Locale);
        }

        [Fact]
        public void Build_ShouldReturnValidConversation()
        {
            // Arrange
            var builder = ConversationBuilder.Create();
            var reference = new ConversationReference
            {
                ChannelId = Channels.Msteams,
                Conversation = new ConversationAccount { Id = "conv123" },
                User = new ChannelAccount { Id = "user123", Name = "Test User" },
                Agent = new ChannelAccount { Id = "agent123", Name = "Test Agent" },
                ServiceUrl = "https://test.com",
                ActivityId = "activity123",
                Locale = "en-US"
            };

            var claims = new Dictionary<string, string>
            {
                { "aud", "audience123" },
                { "appid", "app456" }
            };

            // Act
            var result = builder
                .WithReference(reference)
                .WithClaims(claims)
                .Build();

            // Assert
            Assert.NotNull(result);
            Assert.IsType<Conversation>(result);
            Assert.NotNull(result.Reference);
            Assert.True(result.IsValid());
            Assert.Equal(reference, result.Reference);
            Assert.Equal(2, result.Identity.Claims.ToList().Count);
        }

        [Fact]
        public void FluentInterface_ShouldAllowMethodChaining()
        {
            // Arrange & Act
            var result = ConversationBuilder.Create()
                .WithReference(ConversationReferenceBuilder.Create(Channels.Msteams, "conv123").Build())
                .WithClaimsForClientId("client123", "requestor456")
                .Build();

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Reference);
            Assert.NotNull(result.Identity);
        }

        [Fact]
        public void WithClaims_ShouldReplaceExistingClaims()
        {
            // Arrange
            var builder = ConversationBuilder.Create();
            var reference = new ConversationReference
            {
                ChannelId = Channels.Msteams,
                Conversation = new ConversationAccount { Id = "conv123" }
            };

            var initialClaims = new Dictionary<string, string>
            {
                { "aud", "initial" }
            };

            var newClaims = new Dictionary<string, string>
            {
                { "aud", "replaced" },
                { "appid", "new" }
            };

            // Act
            var result = builder
                .WithReference(reference)
                .WithClaims(initialClaims)
                .WithClaims(newClaims)
                .Build();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Identity.Claims.ToList().Count);
            Assert.Equal("replaced", result.Identity.Claims.First(c => c.Type == "aud").Value);
            Assert.Equal("new", result.Identity.Claims.First(c => c.Type == "appid").Value);
        }

        [Fact]
        public void WithReference_ShouldReplaceExistingReference()
        {
            // Arrange
            var builder = ConversationBuilder.Create();
            var reference1 = new ConversationReference
            {
                Conversation = new ConversationAccount { Id = "conv1" }
            };

            var reference2 = new ConversationReference
            {
                Conversation = new ConversationAccount { Id = "conv2" }
            };

            var claims = new Dictionary<string, string>
            {
                { "aud", "audience" }
            };

            // Act
            var result = builder
                .WithReference(reference1)
                .WithReference(reference2)
                .WithClaims(claims)
                .Build();

            // Assert
            Assert.NotNull(result);
            Assert.Equal("conv2", result.Reference.Conversation.Id);
        }

        [Fact]
        public void WithIdentity_EmptyIdentity_ShouldSetEmptyClaims()
        {
            // Arrange
            var builder = ConversationBuilder.Create();
            var identity = new ClaimsIdentity();
            var reference = new ConversationReference
            {
                ChannelId = Channels.Msteams,
                Conversation = new ConversationAccount { Id = "conv123" }
            };

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                builder
                    .WithReference(reference)
                    .WithIdentity(identity)
                    .Build());
        }
    }
}