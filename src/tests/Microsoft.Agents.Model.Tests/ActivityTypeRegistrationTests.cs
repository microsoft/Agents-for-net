// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Model.Tests
{
    public class ActivityTypeRegistrationTests
    {
        // ─── Test helpers ──────────────────────────────────────────────────────

        // Private activity subclasses used as deserialization targets in tests.
        // Each carries [ActivityType] so its registration path can be exercised.

        [ActivityType("test.atrt.custom")]
        private class CustomTestActivity : Activity
        {
            public string CustomData { get; set; }
        }

        private class AlwaysMatchResolver : IActivityTypeResolver
        {
            public int Priority => 10;
            public bool Matches(JsonElement _) => true;
        }

        private class NeverMatchResolver : IActivityTypeResolver
        {
            public int Priority => 10;
            public bool Matches(JsonElement _) => false;
        }

        private class ChannelIdResolver : IActivityTypeResolver
        {
            private readonly string _channelId;
            public int Priority { get; }

            public ChannelIdResolver(string channelId, int priority = 10)
            {
                _channelId = channelId;
                Priority = priority;
            }

            public bool Matches(JsonElement json) =>
                json.TryGetProperty("channelId", out var c) && c.GetString() == _channelId;
        }

        private class ChannelMessageActivity : MessageActivity { }
        private class HighPriorityActivity : Activity { }
        private class LowPriorityActivity : Activity { }
        private class FallbackActivity : Activity { }

        // ─── ActivityTypeAttribute ─────────────────────────────────────────────

        [Fact]
        public void ActivityTypeAttribute_StoresActivityType()
        {
            var attr = new ActivityTypeAttribute("myType");
            Assert.Equal("myType", attr.ActivityType);
        }

        [Fact]
        public void ActivityTypeAttribute_ResolverDefaultsToNull()
        {
            var attr = new ActivityTypeAttribute("myType");
            Assert.Null(attr.Resolver);
        }

        [Fact]
        public void ActivityTypeAttribute_AcceptsResolverType()
        {
            var attr = new ActivityTypeAttribute("myType") { Resolver = typeof(AlwaysMatchResolver) };
            Assert.Equal(typeof(AlwaysMatchResolver), attr.Resolver);
        }

        [Fact]
        public void ActivityTypeAttribute_AppliedToBuiltinSubclass_HasCorrectType()
        {
            var attr = (ActivityTypeAttribute)Attribute.GetCustomAttribute(
                typeof(MessageActivity), typeof(ActivityTypeAttribute));

            Assert.NotNull(attr);
            Assert.Equal(ActivityTypes.Message, attr.ActivityType);
        }

        // ─── ActivityTypeEntry ─────────────────────────────────────────────────

        [Fact]
        public void ActivityTypeEntry_GetResolvers_InitiallyEmpty()
        {
            var entry = new ActivityTypeEntry();
            Assert.Empty(entry.GetResolvers());
        }

        [Fact]
        public void ActivityTypeEntry_BaseType_DefaultsToNull()
        {
            var entry = new ActivityTypeEntry();
            Assert.Null(entry.BaseType);
        }

        [Fact]
        public void ActivityTypeEntry_AddResolver_AppearsInGetResolvers()
        {
            var entry = new ActivityTypeEntry();
            var resolver = new AlwaysMatchResolver();
            entry.AddResolver(resolver, typeof(MessageActivity));

            var resolvers = entry.GetResolvers();

            Assert.Single(resolvers);
            Assert.Same(resolver, resolvers[0].Resolver);
            Assert.Equal(typeof(MessageActivity), resolvers[0].TargetType);
        }

        [Fact]
        public void ActivityTypeEntry_AddResolver_MaintainsDescendingPriorityOrder()
        {
            var entry = new ActivityTypeEntry();
            var low  = new ChannelIdResolver("low",  priority: 5);
            var high = new ChannelIdResolver("high", priority: 20);
            var mid  = new ChannelIdResolver("mid",  priority: 10);

            // Insert in non-priority order.
            entry.AddResolver(low,  typeof(Activity));
            entry.AddResolver(high, typeof(Activity));
            entry.AddResolver(mid,  typeof(Activity));

            var resolvers = entry.GetResolvers();

            Assert.Equal(3, resolvers.Length);
            Assert.Equal(20, resolvers[0].Resolver.Priority);
            Assert.Equal(10, resolvers[1].Resolver.Priority);
            Assert.Equal(5,  resolvers[2].Resolver.Priority);
        }

        [Fact]
        public void ActivityTypeEntry_AddResolver_EqualPriority_AppendedAfterExisting()
        {
            var entry = new ActivityTypeEntry();
            var first  = new ChannelIdResolver("a", priority: 10);
            var second = new ChannelIdResolver("b", priority: 10);

            entry.AddResolver(first,  typeof(Activity));
            entry.AddResolver(second, typeof(Activity));

            var resolvers = entry.GetResolvers();

            Assert.Equal(2, resolvers.Length);
            Assert.Same(first,  resolvers[0].Resolver);
            Assert.Same(second, resolvers[1].Resolver);
        }

        [Fact]
        public void ActivityTypeEntry_GetResolvers_ReturnsSnapshot_NotLiveReference()
        {
            var entry = new ActivityTypeEntry();
            entry.AddResolver(new AlwaysMatchResolver(), typeof(Activity));

            var snapshot1 = entry.GetResolvers();

            // Adding another resolver after the first snapshot should not affect it.
            entry.AddResolver(new NeverMatchResolver(), typeof(Activity));
            var snapshot2 = entry.GetResolvers();

            Assert.Single(snapshot1);
            Assert.Equal(2, snapshot2.Length);
        }

        // ─── Built-in ActivityTypeMap population ──────────────────────────────

        [Theory]
        [InlineData(ActivityTypes.Command,            typeof(CommandActivity))]
        [InlineData(ActivityTypes.CommandResult,      typeof(CommandResultActivity))]
        [InlineData(ActivityTypes.ConversationUpdate, typeof(ConversationUpdateActivity))]
        [InlineData(ActivityTypes.EndOfConversation,  typeof(EndOfConversationActivity))]
        [InlineData(ActivityTypes.Event,              typeof(EventActivity))]
        [InlineData(ActivityTypes.Handoff,            typeof(HandoffActivity))]
        [InlineData(ActivityTypes.InstallationUpdate, typeof(InstallationUpdateActivity))]
        [InlineData(ActivityTypes.Invoke,             typeof(InvokeActivity))]
        [InlineData(ActivityTypes.Message,            typeof(MessageActivity))]
        [InlineData(ActivityTypes.MessageReaction,    typeof(MessageReactionActivity))]
        [InlineData(ActivityTypes.Trace,              typeof(TraceActivity))]
        [InlineData(ActivityTypes.Typing,             typeof(TypingActivity))]
        public void ActivityTypeMap_ContainsAllBuiltinTypes(string typeString, Type expectedType)
        {
            Assert.True(
                ProtocolJsonSerializer.ActivityTypeMap.TryGetValue(typeString, out var entry),
                $"ActivityTypeMap does not contain key '{typeString}'");

            Assert.Equal(expectedType, entry.BaseType);
        }

        // ─── ProtocolJsonSerializer.AddActivityType ────────────────────────────

        [Fact]
        public void AddActivityType_RegistersBaseType()
        {
            ProtocolJsonSerializer.AddActivityType("test.addBase", typeof(CustomTestActivity));

            Assert.True(ProtocolJsonSerializer.ActivityTypeMap.TryGetValue("test.addBase", out var entry));
            Assert.Equal(typeof(CustomTestActivity), entry.BaseType);
        }

        [Fact]
        public void AddActivityType_LookupIsCaseInsensitive()
        {
            ProtocolJsonSerializer.AddActivityType("Test.CaseInsensitive", typeof(CustomTestActivity));

            Assert.True(ProtocolJsonSerializer.ActivityTypeMap.TryGetValue("test.caseinsensitive", out _));
            Assert.True(ProtocolJsonSerializer.ActivityTypeMap.TryGetValue("TEST.CASEINSENSITIVE", out _));
        }

        [Fact]
        public void AddActivityType_OverwritesExistingBaseType()
        {
            ProtocolJsonSerializer.AddActivityType("test.overwrite", typeof(CustomTestActivity));
            ProtocolJsonSerializer.AddActivityType("test.overwrite", typeof(MessageActivity));

            Assert.Equal(typeof(MessageActivity),
                ProtocolJsonSerializer.ActivityTypeMap["test.overwrite"].BaseType);
        }

        [Fact]
        public void AddActivityType_NonActivitySubclass_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                ProtocolJsonSerializer.AddActivityType("test.invalid", typeof(string)));
        }

        // ─── ProtocolJsonSerializer.AddActivityResolver ────────────────────────

        [Fact]
        public void AddActivityResolver_AppearsInEntryResolvers()
        {
            var resolver = new AlwaysMatchResolver();
            ProtocolJsonSerializer.AddActivityResolver("test.addResolver", typeof(CustomTestActivity), resolver);

            Assert.True(ProtocolJsonSerializer.ActivityTypeMap.TryGetValue("test.addResolver", out var entry));
            Assert.Contains(entry.GetResolvers(), r => ReferenceEquals(r.Resolver, resolver));
        }

        [Fact]
        public void AddActivityResolver_CreatesEntryIfAbsent()
        {
            // Key has not been added before via AddActivityType.
            ProtocolJsonSerializer.AddActivityResolver("test.resolverOnly", typeof(CustomTestActivity),
                new AlwaysMatchResolver());

            Assert.True(ProtocolJsonSerializer.ActivityTypeMap.ContainsKey("test.resolverOnly"));
        }

        [Fact]
        public void AddActivityResolver_NullResolver_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                ProtocolJsonSerializer.AddActivityResolver("test.nullRes", typeof(CustomTestActivity), null));
        }

        [Fact]
        public void AddActivityResolver_NonActivitySubclass_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                ProtocolJsonSerializer.AddActivityResolver("test.badType", typeof(string),
                    new AlwaysMatchResolver()));
        }

        // ─── ActivityConverter deserialization — built-in types ────────────────

        // Only includes types whose subclasses have a public parameterless constructor;
        // ConnectorConverter requires this. Event, Handoff, InstallationUpdate, and Trace
        // only expose parameterised constructors and cannot be deserialized without specific fields.
        [Theory]
        [InlineData(ActivityTypes.Command,            typeof(CommandActivity))]
        [InlineData(ActivityTypes.CommandResult,      typeof(CommandResultActivity))]
        [InlineData(ActivityTypes.ConversationUpdate, typeof(ConversationUpdateActivity))]
        [InlineData(ActivityTypes.EndOfConversation,  typeof(EndOfConversationActivity))]
        [InlineData(ActivityTypes.Invoke,             typeof(InvokeActivity))]
        [InlineData(ActivityTypes.Message,            typeof(MessageActivity))]
        [InlineData(ActivityTypes.MessageReaction,    typeof(MessageReactionActivity))]
        [InlineData(ActivityTypes.Typing,             typeof(TypingActivity))]
        public void Deserialize_BuiltinType_ReturnsCorrectSubclass(string type, Type expectedType)
        {
            var json = $$"""{"type":"{{type}}"}""";

            var activity = ProtocolJsonSerializer.ToObject<Activity>(json);

            Assert.IsType(expectedType, activity);
            Assert.Equal(type, activity.Type);
        }

        [Fact]
        public void Deserialize_UnknownType_ReturnsBaseActivity()
        {
            var json = """{"type":"completely.unknown.99"}""";

            var activity = ProtocolJsonSerializer.ToObject<Activity>(json);

            Assert.IsType<Activity>(activity);
            Assert.Equal("completely.unknown.99", activity.Type);
        }

        [Fact]
        public void Deserialize_TypeMatchIsCaseInsensitive()
        {
            var json = """{"type":"MESSAGE"}""";

            var activity = ProtocolJsonSerializer.ToObject<Activity>(json);

            Assert.IsType<MessageActivity>(activity);
        }

        // ─── ActivityConverter deserialization — custom registered types ───────

        [Fact]
        public void Deserialize_CustomRegisteredType_ReturnsCustomSubclass()
        {
            ProtocolJsonSerializer.AddActivityType("test.deser.custom", typeof(CustomTestActivity));

            var json = """{"type":"test.deser.custom","customData":"hello"}""";
            var activity = ProtocolJsonSerializer.ToObject<Activity>(json);

            var custom = Assert.IsType<CustomTestActivity>(activity);
            Assert.Equal("hello", custom.CustomData);
        }

        [Fact]
        public void Deserialize_ActivityProperties_ArePreserved()
        {
            var json = """{"type":"message","text":"hello","locale":"en-US"}""";

            var activity = ProtocolJsonSerializer.ToObject<Activity>(json);

            var message = Assert.IsType<MessageActivity>(activity);
            Assert.Equal("hello", message.Text);
            Assert.Equal("en-US", message.Locale);
        }

        // ─── ActivityConverter deserialization — resolver paths ───────────────

        [Fact]
        public void Deserialize_Resolver_WhenMatches_ReturnsResolvedType()
        {
            ProtocolJsonSerializer.AddActivityType("test.deser.match", typeof(FallbackActivity));
            ProtocolJsonSerializer.AddActivityResolver("test.deser.match", typeof(ChannelMessageActivity),
                new ChannelIdResolver("resolveChannel"));

            var json = """{"type":"test.deser.match","channelId":"resolveChannel"}""";
            var activity = ProtocolJsonSerializer.ToObject<Activity>(json);

            Assert.IsType<ChannelMessageActivity>(activity);
        }

        [Fact]
        public void Deserialize_Resolver_WhenNoMatch_FallsBackToBaseType()
        {
            ProtocolJsonSerializer.AddActivityType("test.deser.nomatch", typeof(FallbackActivity));
            ProtocolJsonSerializer.AddActivityResolver("test.deser.nomatch", typeof(ChannelMessageActivity),
                new ChannelIdResolver("resolveChannel"));

            var json = """{"type":"test.deser.nomatch","channelId":"otherChannel"}""";
            var activity = ProtocolJsonSerializer.ToObject<Activity>(json);

            Assert.IsType<FallbackActivity>(activity);
        }

        [Fact]
        public void Deserialize_HigherPriorityResolver_CheckedBeforeLowerPriority()
        {
            // Two resolvers for the same type string, different channel IDs, different priorities.
            var lowRes  = new ChannelIdResolver("low.channel",  priority: 5);
            var highRes = new ChannelIdResolver("high.channel", priority: 20);

            ProtocolJsonSerializer.AddActivityType("test.deser.priority", typeof(Activity));
            // Register low first, then high — internal sort must put high first.
            ProtocolJsonSerializer.AddActivityResolver("test.deser.priority", typeof(LowPriorityActivity),  lowRes);
            ProtocolJsonSerializer.AddActivityResolver("test.deser.priority", typeof(HighPriorityActivity), highRes);

            var jsonHigh = """{"type":"test.deser.priority","channelId":"high.channel"}""";
            Assert.IsType<HighPriorityActivity>(ProtocolJsonSerializer.ToObject<Activity>(jsonHigh));

            var jsonLow = """{"type":"test.deser.priority","channelId":"low.channel"}""";
            Assert.IsType<LowPriorityActivity>(ProtocolJsonSerializer.ToObject<Activity>(jsonLow));

            var jsonNone = """{"type":"test.deser.priority","channelId":"other"}""";
            Assert.IsType<Activity>(ProtocolJsonSerializer.ToObject<Activity>(jsonNone));
        }

        [Fact]
        public void Deserialize_Resolver_TakesPriorityOverBaseType()
        {
            // Even when a BaseType is registered, an always-matching resolver wins.
            ProtocolJsonSerializer.AddActivityType("test.deser.resolverWins", typeof(FallbackActivity));
            ProtocolJsonSerializer.AddActivityResolver("test.deser.resolverWins", typeof(ChannelMessageActivity),
                new AlwaysMatchResolver());

            var json = """{"type":"test.deser.resolverWins"}""";
            var activity = ProtocolJsonSerializer.ToObject<Activity>(json);

            Assert.IsType<ChannelMessageActivity>(activity);
        }

        [Fact]
        public void Deserialize_NeverMatchResolver_BaseTypeUsed()
        {
            ProtocolJsonSerializer.AddActivityType("test.deser.neverMatch", typeof(FallbackActivity));
            ProtocolJsonSerializer.AddActivityResolver("test.deser.neverMatch", typeof(ChannelMessageActivity),
                new NeverMatchResolver());

            var json = """{"type":"test.deser.neverMatch"}""";
            var activity = ProtocolJsonSerializer.ToObject<Activity>(json);

            Assert.IsType<FallbackActivity>(activity);
        }

        // ─── Thread safety ─────────────────────────────────────────────────────

        [Fact]
        public async Task AddActivityType_ConcurrentRegistrations_DoNotThrow()
        {
            var tasks = Enumerable.Range(0, 20)
                .Select(i => Task.Run(() =>
                    ProtocolJsonSerializer.AddActivityType(
                        $"test.concurrent.type.{i}", typeof(CustomTestActivity))));

            await Task.WhenAll(tasks);

            Assert.True(ProtocolJsonSerializer.ActivityTypeMap.ContainsKey("test.concurrent.type.0"));
            Assert.True(ProtocolJsonSerializer.ActivityTypeMap.ContainsKey("test.concurrent.type.19"));
        }

        [Fact]
        public async Task AddActivityResolver_ConcurrentRegistrations_DoNotThrow()
        {
            var tasks = Enumerable.Range(0, 20)
                .Select(i => Task.Run(() =>
                    ProtocolJsonSerializer.AddActivityResolver(
                        "test.concurrent.resolver",
                        typeof(CustomTestActivity),
                        new ChannelIdResolver($"ch{i}"))));

            await Task.WhenAll(tasks);

            Assert.Equal(20,
                ProtocolJsonSerializer.ActivityTypeMap["test.concurrent.resolver"].GetResolvers().Length);
        }

        [Fact]
        public async Task ActivityTypeEntry_ConcurrentAddResolvers_DoNotThrow()
        {
            var entry = new ActivityTypeEntry();
            var tasks = Enumerable.Range(0, 20)
                .Select(_ => Task.Run(() =>
                    entry.AddResolver(new AlwaysMatchResolver(), typeof(Activity))));

            await Task.WhenAll(tasks);

            Assert.Equal(20, entry.GetResolvers().Length);
        }
    }
}
