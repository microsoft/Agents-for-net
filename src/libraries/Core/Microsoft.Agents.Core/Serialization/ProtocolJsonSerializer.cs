// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Reflection;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization.Converters;

namespace Microsoft.Agents.Core.Serialization
{
    /// <summary>
    /// Extensions for converting objects to desired types using serialization.
    /// </summary>
    public static class ProtocolJsonSerializer
    {
        public const string ApplicationJson = "application/json";
        public static JsonSerializerOptions SerializationOptions { get; private set; } = InitSerializerOptions();
        public static bool UnpackObjectStrings { get; set; } = true;

        /// <summary>
        /// Provides a way to turn off the {channelId}:{product} notation.  If false,
        /// ChannelId.ToString() is just the {channelId} value.  However, serialization of the 
        /// ProductInfo Entity is still accounted for.  ChannelId.SubChannel is still populated
        /// with the ProductInfo.Id value in any case.
        /// It is not recommended to set false without guidance.
        /// </summary>
        public static bool ChannelIdIncludesProduct { get; set; } = true;

        /// <summary>
        /// Maintains a mapping of entity type names to their corresponding Type objects.
        /// </summary>
        public static ConcurrentDictionary<string, Type> EntityTypes { get; private set; } = CoreEntities();

        /// <summary>
        /// Maps activity <c>type</c> strings to custom <see cref="Activity"/> subclasses for the
        /// simple type-only case. Retained for direct inspection; declarative discriminators
        /// (including channelId/name) are held in the internal registration list. Populated by
        /// <see cref="RegisterActivityTypes"/>.
        /// </summary>
        internal static readonly object _activityResolutionLock = new object();
        private static ActivityTypeRegistration[] _activityRegistrations = Array.Empty<ActivityTypeRegistration>();
        private static ActivityTypeResolver[] _activityResolvers = Array.Empty<ActivityTypeResolver>();

        /// <summary>
        /// True when any custom Activity resolution (declarative registrations or imperative
        /// resolvers) has been registered. Lets the <c>ActivityConverter</c> keep a zero-overhead
        /// fast path when no custom Activity types are in play.
        /// </summary>
        internal static bool HasActivityTypeRegistrations
            => _activityRegistrations.Length != 0 || _activityResolvers.Length != 0;

        private static readonly object _optionsLock = new object();

        static ProtocolJsonSerializer()
        {
            SerializationInitAssemblyAttribute.InitSerialization();
            EntityInitAssemblyAttribute.InitSerialization();
            ActivityTypeInitAssemblyAttribute.InitSerialization();
        }

        private static JsonSerializerOptions InitSerializerOptions()
        {
            var options = new JsonSerializerOptions()
                .ApplyCoreOptions();

            options.TypeInfoResolver = JsonTypeInfoResolver.Combine(
                CoreJsonContext.Default,
                new DefaultJsonTypeInfoResolver());

            return options;
        }

        private static ConcurrentDictionary<string, Type> CoreEntities()
        {
            var entities = new ConcurrentDictionary<string, Type>();
            entities[Models.EntityTypes.ActivityTreatment] = typeof(ActivityTreatment);
            entities[Models.EntityTypes.AICitation] = typeof(AIEntity);
            entities[Models.EntityTypes.GeoCoordinates] = typeof(GeoCoordinates);
            entities[Models.EntityTypes.Mention] = typeof(Mention);
            entities[Models.EntityTypes.Place] = typeof(Place);
            entities[Models.EntityTypes.ProductInfo] = typeof(ProductInfo);
            entities[Models.EntityTypes.StreamInfo] = typeof(StreamInfo);
            entities[Models.EntityTypes.Thing] = typeof(Thing);
            return entities;
        }

        public static void ApplyExtensionConverters(IList<JsonConverter> extensionConverters)
        {
            lock (_optionsLock)
            {
                // Always copy to avoid race with concurrent readers freezing the instance.
                var newOptions = new JsonSerializerOptions(SerializationOptions);

                foreach (var converter in extensionConverters)
                {
                    newOptions.Converters.Add(converter);
                }

                SerializationOptions = newOptions;
            }
        }

        /// <summary>
        /// Applies a transformation function to <see cref="Microsoft.Agents.Core.Serialization.ProtocolJsonSerializer.SerializationOptions"/>, replacing it with
        /// the result. This is an advanced escape hatch — prefer <see cref="Microsoft.Agents.Core.Serialization.ProtocolJsonSerializer.ApplyExtensionConverters"/>
        /// or <see cref="Microsoft.Agents.Core.Serialization.ProtocolJsonSerializer.AddTypeInfoResolver"/> for typical extensions.
        /// </summary>
        /// <param name="applyFunc">
        /// A function that receives the current options and returns the new options.
        /// </param>
        /// <remarks>
        /// <para>
        /// <b>Important:</b> If your function replaces <see cref="System.Text.Json.JsonSerializerOptions.TypeInfoResolver"/>,
        /// you must include <c>CoreJsonContext.Default</c> in the new resolver chain.
        /// Omitting it silently removes source-generated metadata for all core model types.
        /// Use <see cref="System.Text.Json.Serialization.Metadata.JsonTypeInfoResolver.Combine(System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver[])"/> to chain resolvers:
        /// <code>
        /// options.TypeInfoResolver = JsonTypeInfoResolver.Combine(
        ///     YourContext.Default,
        ///     CoreJsonContext.Default,
        ///     new DefaultJsonTypeInfoResolver());
        /// </code>
        /// </para>
        /// </remarks>
        public static void ApplyExtensionOptions(Func<JsonSerializerOptions, JsonSerializerOptions> applyFunc)
        {
            lock (_optionsLock)
            {
                // Always copy to avoid race with concurrent readers freezing the instance.
                var newOptions = new JsonSerializerOptions(SerializationOptions);

                SerializationOptions = applyFunc(newOptions);
            }
        }

        /// <summary>
        /// Prepends a <see cref="System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver"/> (e.g., a source-generated
        /// <see cref="System.Text.Json.Serialization.JsonSerializerContext"/>) to the resolver chain
        /// used by <see cref="Microsoft.Agents.Core.Serialization.ProtocolJsonSerializer.SerializationOptions"/>. The resolver is consulted before any previously
        /// registered resolvers and before the reflection fallback.
        /// Call from a <see cref="Microsoft.Agents.Core.Serialization.SerializationInitAssemblyAttribute"/>-decorated <c>Init()</c> method.
        /// </summary>
        /// <remarks>
        /// Each call prepends the new resolver at the front of the chain.
        /// <see cref="System.Text.Json.Serialization.Metadata.JsonTypeInfoResolver.Combine(System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver[])"/> returns the first non-null result in order,
        /// so the most-recently-added resolver wins for any given type.
        /// </remarks>
        public static void AddTypeInfoResolver(IJsonTypeInfoResolver resolver)
        {
            lock (_optionsLock)
            {
                // Always copy: a concurrent reader (e.g., a parallel test or another thread
                // calling JsonSerializer) can freeze the current instance between our read
                // and the TypeInfoResolver assignment below, causing InvalidOperationException.
                var newOptions = new JsonSerializerOptions(SerializationOptions);

                newOptions.TypeInfoResolver = JsonTypeInfoResolver.Combine(
                    resolver,
                    newOptions.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver());

                SerializationOptions = newOptions;
            }
        }

        public static void AddEntityType(string entityTypeName, Type entityType)
        {
            EntityTypes[entityTypeName] = entityType;
        }

        /// <summary>
        /// Registers custom <see cref="Activity"/> subclasses for polymorphic deserialization.
        /// Each type must derive from <see cref="Activity"/> and be annotated with one or more
        /// <see cref="ActivityTypeAttribute"/>. Each attribute declares the discriminators
        /// (<c>type</c>, and optionally <c>channelId</c> and/or <c>name</c>) that an inbound Activity
        /// must match for the subclass to be used.
        /// </summary>
        /// <remarks>
        /// Annotated subclasses are normally auto-registered at assembly load time (the
        /// <c>ActivityTypeInitSourceGenerator</c> emits an <see cref="ActivityTypeInitAssemblyAttribute"/>
        /// per <c>[ActivityType]</c> class), so calling this directly is rarely needed. It remains public
        /// for explicit/dynamic registration. Registration is idempotent — registering the same type and
        /// discriminators more than once is a no-op.
        /// </remarks>
        /// <param name="types">The annotated <see cref="Activity"/> subclasses to register.</param>
        /// <exception cref="ArgumentException">
        /// A supplied type does not derive from <see cref="Activity"/>, or one of its
        /// <see cref="ActivityTypeAttribute"/> declarations sets none of Type/ChannelId/Name.
        /// </exception>
        public static void RegisterActivityTypes(IEnumerable<Type> types)
        {
            if (types == null)
            {
                return;
            }

            var additions = new List<ActivityTypeRegistration>();

            foreach (var type in types)
            {
                if (!typeof(Activity).IsAssignableFrom(type))
                {
                    throw new ArgumentException($"{type.Name} must derive from {nameof(Activity)}.", nameof(types));
                }

                foreach (var attr in type.GetCustomAttributes<ActivityTypeAttribute>(false))
                {
                    if (string.IsNullOrWhiteSpace(attr.Type)
                        && string.IsNullOrWhiteSpace(attr.ChannelId)
                        && string.IsNullOrWhiteSpace(attr.Name))
                    {
                        throw new ArgumentException(
                            $"[{nameof(ActivityTypeAttribute)}] on {type.Name} must set at least one of Type, ChannelId, or Name.",
                            nameof(types));
                    }

                    additions.Add(new ActivityTypeRegistration(type, attr.Type, attr.ChannelId, attr.Name));
                }
            }

            if (additions.Count == 0)
            {
                return;
            }

            lock (_activityResolutionLock)
            {
                var existing = _activityRegistrations;

                // Deduplicate so re-processing (e.g. auto-registration running for an assembly that
                // is also registered manually, or an assembly seen by both the initial scan and the
                // AssemblyLoad handler) does not append identical registrations.
                var toAdd = new List<ActivityTypeRegistration>();
                foreach (var addition in additions)
                {
                    if (!ContainsRegistration(existing, addition) && !ContainsRegistration(toAdd, addition))
                    {
                        toAdd.Add(addition);
                    }
                }

                if (toAdd.Count == 0)
                {
                    return;
                }

                var updated = new ActivityTypeRegistration[existing.Length + toAdd.Count];
                Array.Copy(existing, updated, existing.Length);
                for (var i = 0; i < toAdd.Count; i++)
                {
                    updated[existing.Length + i] = toAdd[i];
                }

                _activityRegistrations = updated;
            }
        }

        private static bool ContainsRegistration(IReadOnlyList<ActivityTypeRegistration> list, ActivityTypeRegistration candidate)
        {
            for (var i = 0; i < list.Count; i++)
            {
                var r = list[i];
                if (r.ClrType == candidate.ClrType
                    && string.Equals(r.Type, candidate.Type, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(r.ChannelId, candidate.ChannelId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(r.Name, candidate.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Registers an imperative <see cref="ActivityTypeResolver"/> for custom Activity resolution
        /// scenarios that the declarative <see cref="ActivityTypeAttribute"/> discriminators cannot
        /// express. The resolver receives a private <see cref="Utf8JsonReader"/> copy positioned at
        /// the Activity's <c>StartObject</c> (so it can discriminate on any property, including nested
        /// ones) plus the well-known peeked discriminators for convenience. Resolvers are consulted
        /// (in registration order) before declarative registrations; the first resolver to return a
        /// non-<see langword="null"/> type wins.
        /// </summary>
        /// <param name="resolver">The resolver to add.</param>
        /// <exception cref="ArgumentNullException"><paramref name="resolver"/> is <see langword="null"/>.</exception>
        public static void RegisterActivityTypeResolver(ActivityTypeResolver resolver)
        {
            AssertionHelpers.ThrowIfNull(resolver, nameof(resolver));

            lock (_activityResolutionLock)
            {
                var updated = new ActivityTypeResolver[_activityResolvers.Length + 1];
                Array.Copy(_activityResolvers, updated, _activityResolvers.Length);
                updated[_activityResolvers.Length] = resolver;
                _activityResolvers = updated;
            }
        }

        /// <summary>
        /// Resolves the custom <see cref="Activity"/> subclass to deserialize into for the given
        /// peeked discriminators, or <see langword="null"/> to use the base <see cref="Activity"/>.
        /// Imperative resolvers are consulted first (in registration order); then the most-specific
        /// matching declarative registration (greatest number of set discriminators; ties resolved
        /// by registration order).
        /// </summary>
        internal static Type ResolveActivityType(ref Utf8JsonReader reader, in ActivityResolutionContext context)
        {
            // Snapshot the copy-on-write arrays for a consistent, lock-free read.
            var resolvers = _activityResolvers;
            for (var i = 0; i < resolvers.Length; i++)
            {
                // Hand each resolver its own copy of the reader (positioned at StartObject) so its
                // scanning can't disturb deserialization or the next resolver.
                var resolverReader = reader;
                var resolved = resolvers[i](ref resolverReader, in context);
                if (resolved != null)
                {
                    return resolved;
                }
            }

            var registrations = _activityRegistrations;
            ActivityTypeRegistration best = null;
            for (var i = 0; i < registrations.Length; i++)
            {
                var registration = registrations[i];
                if (registration.Matches(in context)
                    && (best == null || registration.Specificity > best.Specificity))
                {
                    best = registration;
                }
            }

            return best?.ClrType;
        }

        /// <summary>
        /// Removes all custom Activity type registrations and resolvers. Intended for test isolation.
        /// </summary>
        internal static void ClearActivityTypeRegistrations()
        {
            lock (_activityResolutionLock)
            {
                _activityRegistrations = Array.Empty<ActivityTypeRegistration>();
                _activityResolvers = Array.Empty<ActivityTypeResolver>();
            }
        }

        private static JsonSerializerOptions ApplyCoreOptions(this JsonSerializerOptions options)
        {
            options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.PropertyNameCaseInsensitive = true;
            options.IncludeFields = true;
            options.NumberHandling = JsonNumberHandling.AllowReadingFromString;

            options.Converters.Add(new ActivityConverter());
            options.Converters.Add(new IActivityConverter());
            options.Converters.Add(new EntityConverter());

            return options;
        }

        /// <summary>
        /// Object to JsonElement conversion.
        /// </summary>
        /// <param name="value">The object to convert to a <see cref="System.Text.Json.JsonElement"/>.</param>
        /// <returns>A <see cref="System.Text.Json.JsonElement"/> representing the specified object.</returns>
        public static JsonElement ToJsonElement(this object value)
        {
            return ToObject<JsonElement>(value);
        }

        /// <summary>
        /// Decompose an object into its constituent JSON elements.
        /// </summary>
        /// <param name="value">The object to be decomposed into JSON elements.</param>
        /// <returns>A dictionary of JSON elements keyed by property name.</returns>
        public static IDictionary<string, JsonElement> ToJsonElements(this object value)
        {
            if (value == null)
            {
                return new Dictionary<string, JsonElement>();
            }

            if (value is Dictionary<string, JsonElement> result)
            {
                return result;
            }

            var elements = new Dictionary<string, JsonElement>();

            if (value is string json)
            {
                if (!string.IsNullOrWhiteSpace(json))
                {
                    using var document = JsonDocument.Parse(json);
                    foreach (var property in document.RootElement.Clone().EnumerateObject())
                    {
                        elements.Add(property.Name, property.Value);
                    }
                }
            }
            else
            {
                var serialized = JsonSerializer.Serialize(value, SerializationOptions);
                using var document = JsonDocument.Parse(serialized);
                foreach (var property in document.RootElement.Clone().EnumerateObject())
                {
                    elements.Add(property.Name, property.Value);
                }
            }

            return elements;
        }

        public static void Add(this IDictionary<string, JsonElement> target, object value)
        {
            var elements = value.ToJsonElements();
            foreach (var prop in elements)
            {
                target.Add(prop.Key, prop.Value);
            }
        }

        /// <summary>
        /// Convert an object to the desired type using serialization and deserialization.
        /// </summary>
        /// <param name="value">The object to be converted to desired type: string, MemoryStream, object</param>
        /// <param name="defaultFactory"></param>
        /// <typeparam name="T">The type of object to convert to.</typeparam>
        /// <returns>The converted object.</returns>
        public static T ToObject<T>(object value, Func<T> defaultFactory = null)
        {
            if (value == null)
            {
                if (defaultFactory != null)
                {
                    return defaultFactory();
                }

                return default;
            }

            if (value is T result)
            {
                return result;
            }

            if (value is string json)
            {
                if (string.IsNullOrWhiteSpace(json))
                {
                    if (defaultFactory != null)
                    {
                        return defaultFactory();
                    }

                    return default;
                }

                return JsonSerializer.Deserialize<T>(json, SerializationOptions);
            }
            else if (value is Stream stream)
            {
                return JsonSerializer.Deserialize<T>(stream, SerializationOptions);
            }
            else if (value is JsonElement jsonElement)
            {
                return JsonSerializer.Deserialize<T>(jsonElement, SerializationOptions);
            }
            else if (value is JsonObject jsonObject)
            {
                return JsonSerializer.Deserialize<T>(jsonObject, SerializationOptions);
            }
            else if (value is JsonNode jsonNode)
            {
                return JsonSerializer.Deserialize<T>(jsonNode, SerializationOptions);
            }

            JsonElement serialized = JsonSerializer.SerializeToElement(value, value.GetType(), SerializationOptions);
            return JsonSerializer.Deserialize<T>(serialized, SerializationOptions);
        }

        public static bool Equals<T>(T value1, T value2)
        {
            return string.Equals(
                    JsonSerializer.Serialize(value1, SerializationOptions),
                    JsonSerializer.Serialize(value2, SerializationOptions)
                );
        }

        public static T CloneTo<T>(object obj)
        {
            return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(obj, SerializationOptions), SerializationOptions);
        }

        public static string ToJson(object value)
        {
            return JsonSerializer.Serialize(value, SerializationOptions);
        }

        public static ToT GetAs<ToT, FromT>(FromT source)
        {
            return JsonSerializer.Deserialize<ToT>(JsonSerializer.Serialize(source, SerializationOptions), SerializationOptions);
        }
    }
}
