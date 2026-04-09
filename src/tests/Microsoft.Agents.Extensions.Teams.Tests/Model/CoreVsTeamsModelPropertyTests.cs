// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Core = Microsoft.Agents.Core;
using Microsoft.Agents.Core.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Agents.Extensions.Teams.Tests.Model
{
    /// <summary>
    /// Enumerates every public model type in Microsoft.Teams.Api, matches each one to a
    /// corresponding Microsoft.Agents.Core.Models type (where one exists), then compares their
    /// JSON-visible property sets.
    ///
    /// All differences are logged via <see cref="ITestOutputHelper"/>.
    ///
    /// ERROR condition: a Teams model has JSON properties not present in the paired Core model,
    /// AND the Core model lacks BOTH:
    ///   (1) a <c>Properties</c> catch-all (<see cref="IDictionary{TKey,TValue}">IDictionary&lt;string,JsonElement&gt;</see>), AND
    ///   (2) a <see cref="JsonConverter"/> registered in <see cref="ProtocolJsonSerializer.SerializationOptions"/>.
    /// When both are present the converter routes unknown properties into <c>Properties</c>, so no
    /// data is lost.
    /// </summary>
    public class CoreVsTeamsModelPropertyTests(ITestOutputHelper output)
    {
        // -----------------------------------------------------------------------
        // Explicit (Teams type → Core type) mappings for cases where the simple
        // class names differ between the two assemblies.
        // Types whose simple names match auto-resolve; this table only covers
        // name-mismatches and known ambiguities.
        // -----------------------------------------------------------------------
        private static readonly IReadOnlyDictionary<Type, Type> ExplicitMappings =
            new Dictionary<Type, Type>
            {
                // ChannelAccount / ConversationAccount
                [typeof(Microsoft.Teams.Api.Account)]      = typeof(Core.Models.ChannelAccount),
                [typeof(Microsoft.Teams.Api.Conversation)] = typeof(Core.Models.ConversationAccount),

                // MessageReaction
#pragma warning disable ExperimentalTeamsReactions // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                [typeof(Microsoft.Teams.Api.Messages.Reaction)] = typeof(Core.Models.MessageReaction),
#pragma warning restore ExperimentalTeamsReactions // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

                // Token types — Teams.Api.Token.Response maps to Core TokenResponse;
                // Teams.Api.Auth.TokenResponse is an OAuth bearer token (different shape)
                // and is intentionally excluded from auto-matching via ExcludeFromAutoMatch.
                [typeof(Microsoft.Teams.Api.Token.Response)] = typeof(Core.Models.TokenResponse),

                // TokenExchange (names in Teams namespace: InvokeRequest / InvokeResponse)
                [typeof(Microsoft.Teams.Api.TokenExchange.InvokeRequest)]  = typeof(Core.Models.TokenExchangeInvokeRequest),
                [typeof(Microsoft.Teams.Api.TokenExchange.InvokeResponse)] = typeof(Core.Models.TokenExchangeInvokeResponse),

                // Card types — same base name, different parent namespace
                [typeof(Microsoft.Teams.Api.Cards.Action)]   = typeof(Core.Models.CardAction),
                [typeof(Microsoft.Teams.Api.Cards.Image)]    = typeof(Core.Models.CardImage),
                // SignInCard vs SigninCard (capitalisation differs)
                [typeof(Microsoft.Teams.Api.Cards.SignInCard)] = typeof(Core.Models.SigninCard),

                // Entity subclasses — Teams uses "...Entity" suffix; Core doesn't.
                // Teams.Api.Messages.Mention is a different type (Teams-only message construct)
                // and is excluded from auto-matching via ExcludeFromAutoMatch.
                [typeof(Microsoft.Teams.Api.Entities.MentionEntity)]    = typeof(Core.Models.Mention),
                [typeof(Microsoft.Teams.Api.Entities.StreamInfoEntity)] = typeof(Core.Models.StreamInfo),
                [typeof(Microsoft.Teams.Api.Entities.CitationEntity)]   = typeof(Core.Models.AIEntity),
            };

        // -----------------------------------------------------------------------
        // Per-Teams-type JSON property names to exclude from the comparison.
        // Use this for structural/schema fields that are not data properties and
        // do not need to be preserved by the Core model (e.g. JSON-LD context).
        // Keys are Teams types; values are the JSON property names to ignore.
        // -----------------------------------------------------------------------
        private static readonly IReadOnlyDictionary<Type, IReadOnlySet<string>> IgnoredProperties =
            new Dictionary<Type, IReadOnlySet<string>>
            {
                // JSON-LD context fields present on all Teams entity types.
                // These are structural schema.org annotations whose values are always
                // fixed by the Teams.Api library; they carry no data that needs to be
                // round-tripped through Core.
                
                // these would be retained by serialization but not a factor for compat
                [typeof(Microsoft.Teams.Api.Entities.MentionEntity)] =
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "@context", "@type" },  
                [typeof(Microsoft.Teams.Api.Entities.StreamInfoEntity)] =
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "@context", "@type" },

                // these are out of spec properties that aren't interesting to us
                [typeof(Microsoft.Teams.Api.Cards.OAuthCard)] =
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "title", "subtitle" },
                [typeof(Microsoft.Teams.Api.Cards.SignInCard)] =
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "title", "subtitle" },
            };

        // -----------------------------------------------------------------------
        // Teams types whose simple name accidentally collides with a Core type name
        // but are semantically unrelated; exclude them from auto-matching.
        // -----------------------------------------------------------------------
        private static readonly IReadOnlySet<string> ExcludeFromAutoMatch = new HashSet<string>
        {
            typeof(Microsoft.Teams.Api.Auth.TokenResponse).FullName, // OAuth bearer response — unrelated to Core.Models.TokenResponse
            typeof(Microsoft.Teams.Api.ChannelId).FullName,          // Just a string wrapper, no Core counterpart
            // Teams.Api.Messages.Mention is a Teams-specific message construct (has required int id,
            // mentionText); Core.Models.Mention maps instead to Teams.Api.Entities.MentionEntity.
            typeof(Microsoft.Teams.Api.Messages.Mention).FullName,
        };

        // -----------------------------------------------------------------------
        // Test
        // -----------------------------------------------------------------------

        [Fact]
        public void AllTeamsApiModelTypes_AreComparedToCoreModels_NoUnhandledAdditionalProperties()
        {
            var sb = new StringBuilder();
            var errors = new List<string>();

            var coreModelsByName = BuildCoreModelsByName();
            var allTeamsTypes    = GetAllTeamsModelTypes();
            var baseActivity     = typeof(Microsoft.Teams.Api.Activities.Activity);

            int matched = 0, unmatched = 0;
            var sortedTeamsTypes = allTeamsTypes.OrderBy(t => t.FullName).ToList();

            sb.AppendLine($"Teams.Api model types found: {sortedTeamsTypes.Count}");
            sb.AppendLine();

            foreach (var teamsType in sortedTeamsTypes)
            {
                var coreType = ResolveCore(teamsType, baseActivity, coreModelsByName);

                if (coreType == null)
                {
                    sb.AppendLine($"[no Core counterpart] {teamsType.FullName}");
                    unmatched++;
                    continue;
                }

                matched++;
                Compare(teamsType, coreType, sb, errors);
            }

            sb.AppendLine();
            sb.AppendLine($"Matched pairs : {matched}");
            sb.AppendLine($"No Core match : {unmatched}");
            if (errors.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("ERRORS:");
                foreach (var e in errors)
                    sb.AppendLine($"  {e}");
            }

            output.WriteLine(sb.ToString());

            Assert.True(
                errors.Count == 0,
                "One or more Teams.Api model types have JSON properties that the paired " +
                "Core.Models type cannot safely preserve:\n" +
                string.Join("\n", errors));
        }

        // -----------------------------------------------------------------------
        // Helpers — mapping
        // -----------------------------------------------------------------------

        private Type ResolveCore(
            Type teamsType,
            Type baseActivity,
            IReadOnlyDictionary<string, Type> coreModelsByName)
        {
            // 1. Explicit override table
            if (ExplicitMappings.TryGetValue(teamsType, out var coreType))
                return coreType;

            // 2. Any Teams Activity subtype (including base) maps to Core.Activity
            if (baseActivity.IsAssignableFrom(teamsType))
                return typeof(Core.Models.Activity);

            // 3. Auto-match by simple type name, unless explicitly excluded
            if (!ExcludeFromAutoMatch.Contains(teamsType.FullName) &&
                coreModelsByName.TryGetValue(teamsType.Name, out coreType))
                return coreType;

            return null;
        }

        /// <summary>
        /// Returns all public non-abstract non-nested model classes from the Microsoft.Teams.Api
        /// assembly that have at least one named, non-ignored, non-catch-all JSON property.
        /// </summary>
        private static List<Type> GetAllTeamsModelTypes()
        {
            var assembly = typeof(Microsoft.Teams.Api.Account).Assembly;

            return assembly.GetExportedTypes()
                .Where(t => t.IsClass && !t.IsAbstract && !t.IsNested)
                .Where(t => !typeof(Attribute).IsAssignableFrom(t))
                .Where(t => !typeof(Exception).IsAssignableFrom(t))
                .Where(t => !typeof(JsonConverter).IsAssignableFrom(t))
                .Where(t => !t.Name.EndsWith("Converter", StringComparison.OrdinalIgnoreCase))
                .Where(t => !t.Name.EndsWith("Factory", StringComparison.OrdinalIgnoreCase))
                .Where(t => !t.Name.EndsWith("Builder", StringComparison.OrdinalIgnoreCase))
                // Must have at least one named, non-ignore, non-catch-all property
                .Where(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                             .Any(p => p.CanRead &&
                                       p.GetCustomAttribute<JsonIgnoreAttribute>() == null &&
                                       !IsCatchAllProperty(p)))
                .ToList();
        }

        /// <summary>
        /// Builds a name→type lookup for all public model classes in
        /// <c>Microsoft.Agents.Core.Models</c>.
        /// </summary>
        private static IReadOnlyDictionary<string, Type> BuildCoreModelsByName()
        {
            var assembly = typeof(Core.Models.Activity).Assembly;

            // Keep the last definition if there are duplicates (shouldn't happen but be safe)
            var dict = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in assembly.GetExportedTypes()
                                      .Where(t => t.IsClass && !t.IsAbstract &&
                                                  t.Namespace == "Microsoft.Agents.Core.Models"))
            {
                dict[t.Name] = t;
            }
            return dict;
        }

        // -----------------------------------------------------------------------
        // Helpers — property comparison
        // -----------------------------------------------------------------------

        private static void Compare(
            Type teamsType, Type coreType,
            StringBuilder sb, List<string> errors)
        {
            var coreNames  = GetNamedJsonProperties(coreType);
            var teamsNames = GetNamedJsonProperties(teamsType);

            IgnoredProperties.TryGetValue(teamsType, out var ignored);

            var teamsOnly = teamsNames.Keys
                .Except(coreNames.Keys, StringComparer.OrdinalIgnoreCase)
                .Where(p => ignored == null || !ignored.Contains(p))
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var coreOnly = coreNames.Keys
                .Except(teamsNames.Keys, StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            sb.AppendLine($"[matched] {teamsType.FullName}");
            sb.AppendLine($"       -> {coreType.FullName}");

            if (teamsOnly.Count == 0 && coreOnly.Count == 0)
            {
                sb.AppendLine("          No differences.");
            }
            else
            {
                if (teamsOnly.Count > 0)
                    sb.AppendLine($"          Teams-only : {string.Join(", ", teamsOnly)}");
                if (coreOnly.Count > 0)
                    sb.AppendLine($"          Core-only  : {string.Join(", ", coreOnly)}");
            }

            if (teamsOnly.Count > 0)
            {
                bool hasDict = HasPropertiesCatchAll(coreType);
                bool hasConv = HasRegisteredConverter(coreType);

                if (hasDict && hasConv)
                {
                    sb.AppendLine("          OK: Teams-only properties preserved via Properties catch-all + converter.");
                }
                else
                {
                    var missing = new List<string>(2);
                    if (!hasDict) missing.Add("Properties<string,JsonElement> catch-all");
                    if (!hasConv) missing.Add("registered JsonConverter");

                    var msg =
                        $"{teamsType.Name} -> {coreType.Name}: " +
                        $"Teams-only [{string.Join(", ", teamsOnly)}] " +
                        $"but Core is missing {string.Join(" and ", missing)}.";

                    sb.AppendLine($"          ERROR: {msg}");
                    errors.Add(msg);
                }
            }
        }

        /// <summary>
        /// Returns the JSON names (via <see cref="JsonPropertyNameAttribute"/> or camelCase)
        /// of all public readable instance properties that are not <see cref="JsonIgnoreAttribute"/>-
        /// annotated and are not the extension-data catch-all.
        /// </summary>
        private static IReadOnlyDictionary<string, PropertyInfo> GetNamedJsonProperties(Type type)
        {
            var dict = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead)
                .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() == null)
                .Where(p => !IsCatchAllProperty(p)))
            {
                // Last write wins — most-derived override takes precedence for duplicate JSON names
                dict[GetJsonName(p)] = p;
            }
            return dict;
        }

        /// <summary>
        /// Returns <see langword="true"/> when <paramref name="prop"/> is a generic-dictionary
        /// extension-data property named <c>Properties</c>.
        /// </summary>
        private static bool IsCatchAllProperty(PropertyInfo prop)
        {
            if (!string.Equals(prop.Name, "Properties", StringComparison.Ordinal)) return false;
            var t = prop.PropertyType;
            if (!t.IsGenericType) return false;
            var def = t.GetGenericTypeDefinition();
            return def == typeof(IDictionary<,>) || def == typeof(Dictionary<,>);
        }

        private static string GetJsonName(PropertyInfo prop)
        {
            var attr = prop.GetCustomAttribute<JsonPropertyNameAttribute>();
            if (attr != null) return attr.Name;
            var name = prop.Name;
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        private static bool HasPropertiesCatchAll(Type type)
        {
            var prop = type.GetProperty("Properties", BindingFlags.Public | BindingFlags.Instance);
            return prop != null &&
                   typeof(IDictionary<string, JsonElement>).IsAssignableFrom(prop.PropertyType);
        }

        private static bool HasRegisteredConverter(Type type)
        {
            return ProtocolJsonSerializer.SerializationOptions.Converters
                .Any(c => c.CanConvert(type));
        }
    }
}
