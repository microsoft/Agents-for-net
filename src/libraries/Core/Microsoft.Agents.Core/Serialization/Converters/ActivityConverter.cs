// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System;
using System.Collections;
using System.Reflection;
using System.Text.Json;

namespace Microsoft.Agents.Core.Serialization.Converters
{
    internal class ActivityConverter : ConnectorConverter<Activity>
    {
        // Claim Activity subclasses too (e.g. custom types resolved via ActivityTypeResolver). The
        // base JsonConverter<Activity> only matches typeof(Activity), which is enough for reading
        // (Read is invoked explicitly with the resolved subclass) but not for top-level writing of a
        // subclass instance — without this, System.Text.Json falls back to default reflection and
        // mis-serializes members like ChannelId. value.GetType() drives the actual property set.
        public override bool CanConvert(Type typeToConvert)
        {
            return typeof(Activity).IsAssignableFrom(typeToConvert);
        }

        public override Activity Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Fast path: no custom activity resolution registered — existing behavior, zero overhead.
            if (!ProtocolJsonSerializer.HasActivityTypeRegistrations)
            {
                return ReadDefault(ref reader, typeToConvert, options);
            }

            // Peek the discriminator fields (on a struct copy, leaving the real reader untouched)
            // to resolve the CLR type to deserialize into.
            var readerCopy = reader;
            var context = PeekDiscriminators(ref readerCopy);

            // Resolvers may scan arbitrary properties, so hand resolution a fresh copy positioned at
            // StartObject (PeekDiscriminators consumed readerCopy). The real reader stays untouched.
            var resolverReader = reader;
            var resolvedType = ProtocolJsonSerializer.ResolveActivityType(ref resolverReader, in context);
            if (resolvedType != null)
            {
                return ReadDefault(ref reader, resolvedType, options);
            }

            return ReadDefault(ref reader, typeToConvert, options);
        }

        private Activity ReadDefault(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var activity = base.Read(ref reader, typeToConvert, options);

            var productInfo = activity.GetProductInfoEntity();
            if (productInfo != null)
            {
                if (activity.ChannelId != null)
                {
                    activity.ChannelId.SubChannel = productInfo.Id;
                }
            }

            return activity;
        }

        private static ActivityResolutionContext PeekDiscriminators(ref Utf8JsonReader reader)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                return default;
            }

            string type = null;
            string channelId = null;
            string name = null;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    break;
                }

                if (reader.TokenType != JsonTokenType.PropertyName || reader.CurrentDepth != 1)
                {
                    // Skip stray values / nested content encountered at the top level.
                    reader.Skip();
                    continue;
                }

                var isType = reader.ValueTextEquals("type"u8);
                var isChannelId = !isType && reader.ValueTextEquals("channelId"u8);
                var isName = !isType && !isChannelId && reader.ValueTextEquals("name"u8);

                if (!reader.Read())
                {
                    break;
                }

                if (isType || isChannelId || isName)
                {
                    var value = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;

                    if (isType)
                    {
                        type = value;
                    }
                    else if (isChannelId)
                    {
                        channelId = ChannelSegment(value);
                    }
                    else
                    {
                        name = value;
                    }

                    if (type != null && channelId != null && name != null)
                    {
                        break;
                    }
                }
                else
                {
                    // Skip the value of an uninteresting property (no-op for scalars,
                    // skips the whole subtree for objects/arrays).
                    reader.Skip();
                }
            }

            return new ActivityResolutionContext(type, channelId, name);
        }

        // The channelId can carry a "{channel}:{product}" sub-channel suffix; discriminators match
        // on the bare channel segment.
        private static string ChannelSegment(string channelId)
        {
            if (channelId == null)
            {
                return null;
            }

            var separator = channelId.IndexOf(':');
            return separator < 0 ? channelId : channelId.Substring(0, separator);
        }

        public override void Write(Utf8JsonWriter writer, Activity value, JsonSerializerOptions options)
        {
            var productInfo = value.GetProductInfoEntity();
            if (value.ChannelId != null && value.ChannelId.IsSubChannel())
            {
                if (productInfo != null)
                {
                    productInfo.Id = value.ChannelId.SubChannel;
                }
                else
                {
                    value.Entities.Add(new ProductInfo() { Id = value.ChannelId.SubChannel });
                }
            }
            else if (productInfo != null)
            {
                value.Entities.Remove(productInfo);
            }

            base.Write(writer, value, options);
        }

        /// <inheritdoc/>
        protected override bool TryReadCollectionProperty(ref Utf8JsonReader reader, Activity value, string propertyName, JsonSerializerOptions options)
        {
            PropertyInfo propertyInfo = typeof(Activity).GetProperty(propertyName);
            if (propertyInfo != null  && propertyInfo.PropertyType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(propertyInfo.PropertyType))
            {
                return true;
            }
            return false;
        }

        protected override void ReadProperty(ref Utf8JsonReader reader, Activity value, string propertyName, JsonSerializerOptions options, PropertyInfo property)
        {
            if (propertyName.Equals("channelId", System.StringComparison.OrdinalIgnoreCase))
            {
                var propertyValue = JsonSerializer.Deserialize<string>(ref reader, options);
                property.SetValue(value, new ChannelId(propertyValue, ProtocolJsonSerializer.ChannelIdIncludesProduct));
                return;
            }

            base.ReadProperty(ref reader, value, propertyName, options, property);
        }

        /// <inheritdoc/>
        protected override bool TryReadGenericProperty(ref Utf8JsonReader reader, Activity value, string propertyName, JsonSerializerOptions options)
        {
            if (propertyName.Equals(nameof(value.ChannelData)))
            {
                SetGenericProperty(ref reader, data => value.ChannelData = data, options);
            }
            else if (propertyName.Equals(nameof(value.Value)))
            {
                SetGenericProperty(ref reader, data => value.Value = data, options);
            }
            else
            {
                return false;
            }

            return true;
        }

        /// <inheritdoc/>
        protected override void ReadExtensionData(ref Utf8JsonReader reader, Activity value, string propertyName, JsonSerializerOptions options)
        {
            var extensionData = JsonSerializer.Deserialize<JsonElement>(ref reader, options);
            value.Properties.Add(propertyName, extensionData);
        }

        /// <inheritdoc/>
        protected override bool TryReadExtensionData(ref Utf8JsonReader reader, Activity value, string propertyName, JsonSerializerOptions options)
        {
            if (!propertyName.Equals(nameof(value.Properties)))
            {
                return false;
            }

            var propertyValue = JsonSerializer.Deserialize<object>(ref reader, options);

            foreach (var element in propertyValue.ToJsonElements())
            {
                value.Properties.Add(element.Key, element.Value);
            }

            return true;
        }

        /// <inheritdoc/>
        protected override bool TryWriteExtensionData(Utf8JsonWriter writer, Activity value, string propertyName)
        {
            if (propertyName.Equals(nameof(value.ChannelId)))
            {
                if (value.ChannelId != null)
                {
                    writer.WriteString("channelId", value.ChannelId.Channel);
                }
                return true;
            }

            if (!propertyName.Equals(nameof(value.Properties)))
            {
                return false;
            }

            foreach (var extensionData in value.Properties)
            {
                writer.WritePropertyName(extensionData.Key);
                extensionData.Value.WriteTo(writer);
            }

            return true;
        }
    }
}
