// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Xunit;

namespace Microsoft.Agents.Model.Tests
{
    public class CustomActivityTypeTests
    {
        // Register custom activity types/resolvers, always clearing afterward to keep the global
        // ProtocolJsonSerializer resolution state from leaking into other tests. Only "x-*" types and
        // fake channel ids are used so concurrent deserialization of real Activities never matches.
        private static IDisposable Register(params Type[] types)
        {
            ProtocolJsonSerializer.RegisterActivityTypes(types);
            return new Cleanup();
        }

        [Fact]
        public void CustomActivityType_ResolvesSubclass()
        {
            using var _ = Register(typeof(WorkflowTriggerActivity));

            var json = """
                {
                  "type": "x-workflowTrigger",
                  "id": "act-1",
                  "workflowId": "wf-42",
                  "correlationId": "corr-9",
                  "parameters": { "region": "west", "count": 3 }
                }
                """;

            var result = ProtocolJsonSerializer.ToObject<Activity>(json);

            var trigger = Assert.IsType<WorkflowTriggerActivity>(result);
            Assert.Equal("x-workflowTrigger", trigger.Type);
            Assert.Equal("act-1", trigger.Id);
            Assert.Equal("wf-42", trigger.WorkflowId);
            Assert.Equal("corr-9", trigger.CorrelationId);
            Assert.NotNull(trigger.Parameters);
            Assert.Equal(2, trigger.Parameters.Count);
        }

        [Fact]
        public void UnregisteredType_FallsBackToActivity()
        {
            // Registry is non-empty, but "message" is never registered -> base Activity.
            using var _ = Register(typeof(WorkflowTriggerActivity));

            var json = """{"type":"message","text":"hello","id":"m-1"}""";

            var result = ProtocolJsonSerializer.ToObject<Activity>(json);

            Assert.Equal(typeof(Activity), result.GetType());
            Assert.Equal("message", result.Type);
            Assert.Equal("hello", result.Text);
        }

        [Fact]
        public void IActivity_AlsoResolvesSubclass()
        {
            using var _ = Register(typeof(WorkflowTriggerActivity));

            var json = """{"type":"x-workflowTrigger","workflowId":"wf-1"}""";

            var result = ProtocolJsonSerializer.ToObject<IActivity>(json);

            var trigger = Assert.IsType<WorkflowTriggerActivity>(result);
            Assert.Equal("wf-1", trigger.WorkflowId);
        }

        [Fact]
        public void MultipleAttributes_RegisterAllTypeStrings()
        {
            using var _ = Register(typeof(MultiTypeActivity));

            var a = ProtocolJsonSerializer.ToObject<Activity>("""{"type":"x-alpha"}""");
            var b = ProtocolJsonSerializer.ToObject<Activity>("""{"type":"x-beta"}""");

            Assert.IsType<MultiTypeActivity>(a);
            Assert.IsType<MultiTypeActivity>(b);
        }

        [Fact]
        public void ShadowedProperty_Deserializes()
        {
            using var _ = Register(typeof(ShadowActivity));

            var json = """{"type":"x-shadow","speak":"strengthened"}""";

            var result = ProtocolJsonSerializer.ToObject<Activity>(json);

            var shadow = Assert.IsType<ShadowActivity>(result);
            Assert.Equal("strengthened", shadow.Speak);
        }

        [Fact]
        public void CustomActivity_RoundTrips()
        {
            using var _ = Register(typeof(WorkflowTriggerActivity));

            var original = new WorkflowTriggerActivity
            {
                Type = "x-workflowTrigger",
                Id = "rt-1",
                WorkflowId = "wf-77",
                CorrelationId = "corr-2",
                Parameters = new Dictionary<string, object> { ["k"] = "v" }
            };

            var json = ProtocolJsonSerializer.ToJson(original);
            var result = ProtocolJsonSerializer.ToObject<Activity>(json);

            var trigger = Assert.IsType<WorkflowTriggerActivity>(result);
            Assert.Equal("wf-77", trigger.WorkflowId);
            Assert.Equal("corr-2", trigger.CorrelationId);
            Assert.NotNull(trigger.Parameters);
            Assert.Single(trigger.Parameters);
        }

        [Fact]
        public void CustomActivityType_ResolvesWhenTypeNotFirst()
        {
            // "type" appears after a nested object — exercises the peek's skip-over-nested logic.
            using var _ = Register(typeof(WorkflowTriggerActivity));

            var json = """
                {
                  "id": "act-2",
                  "parameters": { "region": "east", "nested": { "deep": true } },
                  "channelData": { "foo": "bar" },
                  "type": "x-workflowTrigger",
                  "workflowId": "wf-99"
                }
                """;

            var result = ProtocolJsonSerializer.ToObject<Activity>(json);

            var trigger = Assert.IsType<WorkflowTriggerActivity>(result);
            Assert.Equal("act-2", trigger.Id);
            Assert.Equal("wf-99", trigger.WorkflowId);
            Assert.NotNull(trigger.Parameters);
        }

        [Fact]
        public void ChannelIdDiscriminator_ResolvesSubclass()
        {
            // "message" activities on the fake teams channel resolve to the Teams subclass...
            using var __ = Register(typeof(TeamsChannelMessageActivity));

            var teams = ProtocolJsonSerializer.ToObject<Activity>(
                """{"type":"message","channelId":"x-teams-test","text":"hi"}""");
            Assert.IsType<TeamsChannelMessageActivity>(teams);

            // ...but the same type on a different channel falls back to the base Activity.
            var other = ProtocolJsonSerializer.ToObject<Activity>(
                """{"type":"message","channelId":"x-other-test","text":"hi"}""");
            Assert.Equal(typeof(Activity), other.GetType());
        }

        [Fact]
        public void ChannelIdOnly_MatchesAnyType()
        {
            // No Type discriminator -> any activity on the channel resolves.
            using var __ = Register(typeof(AnyTeamsChannelActivity));

            var message = ProtocolJsonSerializer.ToObject<Activity>(
                """{"type":"message","channelId":"x-anyteams-test"}""");
            var invoke = ProtocolJsonSerializer.ToObject<Activity>(
                """{"type":"invoke","channelId":"x-anyteams-test"}""");

            Assert.IsType<AnyTeamsChannelActivity>(message);
            Assert.IsType<AnyTeamsChannelActivity>(invoke);
        }

        [Fact]
        public void ChannelIdDiscriminator_IgnoresProductSubChannelSuffix()
        {
            // channelId may carry a "{channel}:{product}" suffix; matching is on the channel segment.
            using var __ = Register(typeof(TeamsChannelMessageActivity));

            var result = ProtocolJsonSerializer.ToObject<Activity>(
                """{"type":"message","channelId":"x-teams-test:someProduct","text":"hi"}""");

            Assert.IsType<TeamsChannelMessageActivity>(result);
        }

        [Fact]
        public void NameDiscriminator_ResolvesSubclass()
        {
            // Second-level discrimination on invoke name.
            using var __ = Register(typeof(TaskFetchInvokeActivity));

            var match = ProtocolJsonSerializer.ToObject<Activity>(
                """{"type":"x-invoke","name":"task/fetch"}""");
            var noMatch = ProtocolJsonSerializer.ToObject<Activity>(
                """{"type":"x-invoke","name":"task/submit"}""");

            Assert.IsType<TaskFetchInvokeActivity>(match);
            Assert.Equal(typeof(Activity), noMatch.GetType());
        }

        [Fact]
        public void MostSpecificRegistration_Wins()
        {
            // A channel-only (specificity 1) and a type+channel (specificity 2) registration both
            // target the same channel. The more specific one wins regardless of registration order.
            using var __ = Register(typeof(SpecChannelActivity), typeof(SpecTypeChannelActivity));

            var message = ProtocolJsonSerializer.ToObject<Activity>(
                """{"type":"message","channelId":"x-spec-test"}""");
            var invoke = ProtocolJsonSerializer.ToObject<Activity>(
                """{"type":"invoke","channelId":"x-spec-test"}""");

            // message matches both -> the 2-discriminator registration wins.
            Assert.IsType<SpecTypeChannelActivity>(message);
            // invoke matches only the channel-only registration.
            Assert.IsType<SpecChannelActivity>(invoke);
        }

        [Fact]
        public void CustomResolver_ResolvesSubclass()
        {
            // Fully imperative resolution for logic the attributes can't express.
            ProtocolJsonSerializer.RegisterActivityTypeResolver(
                (ref Utf8JsonReader reader, in ActivityResolutionContext ctx) =>
                    ctx.ChannelId == "x-resolver-test" ? typeof(ResolverActivity) : null);

            try
            {
                var match = ProtocolJsonSerializer.ToObject<Activity>(
                    """{"type":"message","channelId":"x-resolver-test"}""");
                var noMatch = ProtocolJsonSerializer.ToObject<Activity>(
                    """{"type":"message","channelId":"x-elsewhere"}""");

                Assert.IsType<ResolverActivity>(match);
                Assert.Equal(typeof(Activity), noMatch.GetType());
            }
            finally
            {
                ProtocolJsonSerializer.ClearActivityTypeRegistrations();
            }
        }

        [Fact]
        public void CustomResolver_TakesPriorityOverDeclarative()
        {
            using var __ = Register(typeof(TeamsChannelMessageActivity));
            ProtocolJsonSerializer.RegisterActivityTypeResolver(
                (ref Utf8JsonReader reader, in ActivityResolutionContext ctx) =>
                    ctx.ChannelId == "x-teams-test" ? typeof(ResolverActivity) : null);

            var result = ProtocolJsonSerializer.ToObject<Activity>(
                """{"type":"message","channelId":"x-teams-test"}""");

            // Resolver runs before declarative registrations.
            Assert.IsType<ResolverActivity>(result);
        }

        [Fact]
        public void CustomResolver_CanDiscriminateOnNestedProperty()
        {
            // The reader gives resolvers access to ANY property, including nested ones the
            // well-known discriminators (type/channelId/name) don't surface.
            ProtocolJsonSerializer.RegisterActivityTypeResolver(
                (ref Utf8JsonReader reader, in ActivityResolutionContext ctx) =>
                {
                    if (JsonDocument.TryParseValue(ref reader, out var doc))
                    {
                        using (doc)
                        {
                            if (doc.RootElement.TryGetProperty("value", out var value)
                                && value.TryGetProperty("action", out var action)
                                && action.GetString() == "escalate")
                            {
                                return typeof(EscalatedActivity);
                            }
                        }
                    }

                    return null;
                });

            try
            {
                var match = ProtocolJsonSerializer.ToObject<Activity>(
                    """{"type":"invoke","value":{"action":"escalate","ticket":"T-9"}}""");
                var noMatch = ProtocolJsonSerializer.ToObject<Activity>(
                    """{"type":"invoke","value":{"action":"acknowledge"}}""");

                Assert.IsType<EscalatedActivity>(match);
                Assert.Equal(typeof(Activity), noMatch.GetType());
            }
            finally
            {
                ProtocolJsonSerializer.ClearActivityTypeRegistrations();
            }
        }

        [Fact]
        public void CustomResolver_ReaderCopyDoesNotDisturbDeserialization()
        {
            // A resolver that fully consumes its reader copy must not corrupt the actual
            // deserialization of the Activity.
            ProtocolJsonSerializer.RegisterActivityTypeResolver(
                (ref Utf8JsonReader reader, in ActivityResolutionContext ctx) =>
                {
                    // Drain the copy entirely.
                    while (reader.Read())
                    {
                    }

                    return null;
                });

            try
            {
                var result = ProtocolJsonSerializer.ToObject<Activity>(
                    """{"type":"message","text":"hello","id":"m-1"}""");

                Assert.Equal(typeof(Activity), result.GetType());
                Assert.Equal("message", result.Type);
                Assert.Equal("hello", result.Text);
                Assert.Equal("m-1", result.Id);
            }
            finally
            {
                ProtocolJsonSerializer.ClearActivityTypeRegistrations();
            }
        }

        [Fact]
        public void RegisterActivityTypeResolver_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                ProtocolJsonSerializer.RegisterActivityTypeResolver(null));
        }

        [Fact]
        public void RegisterActivityTypes_NoDiscriminator_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                ProtocolJsonSerializer.RegisterActivityTypes(new[] { typeof(NoDiscriminatorActivity) }));
        }

        [Fact]
        public void RegisterActivityTypes_NonActivityType_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                ProtocolJsonSerializer.RegisterActivityTypes(new[] { typeof(NotAnActivity) }));
        }

        [Fact]
        public void RegisterActivityTypes_Null_DoesNotThrow()
        {
            ProtocolJsonSerializer.RegisterActivityTypes(null);
        }

        private sealed class Cleanup : IDisposable
        {
            public void Dispose() => ProtocolJsonSerializer.ClearActivityTypeRegistrations();
        }

        [ActivityType("x-workflowTrigger")]
        private class WorkflowTriggerActivity : Activity
        {
            public string WorkflowId { get; set; }

            public Dictionary<string, object> Parameters { get; set; }

            public string CorrelationId { get; set; }
        }

        [ActivityType("x-alpha")]
        [ActivityType("x-beta")]
        private class MultiTypeActivity : Activity
        {
        }

        [ActivityType("x-shadow")]
        private class ShadowActivity : Activity
        {
            public new string Speak { get; set; }
        }

        [ActivityType("message", ChannelId = "x-teams-test")]
        private class TeamsChannelMessageActivity : Activity
        {
        }

        [ActivityType(ChannelId = "x-anyteams-test")]
        private class AnyTeamsChannelActivity : Activity
        {
        }

        [ActivityType("x-invoke", Name = "task/fetch")]
        private class TaskFetchInvokeActivity : Activity
        {
        }

        [ActivityType(ChannelId = "x-spec-test")]
        private class SpecChannelActivity : Activity
        {
        }

        [ActivityType("message", ChannelId = "x-spec-test")]
        private class SpecTypeChannelActivity : Activity
        {
        }

        private class ResolverActivity : Activity
        {
        }

        private class EscalatedActivity : Activity
        {
        }

        [ActivityType]
        private class NoDiscriminatorActivity : Activity
        {
        }

        private class NotAnActivity
        {
        }
    }
}
