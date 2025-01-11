// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using System.Text.Json;
using System;

namespace Microsoft.Agents.Core.Serialization.Converters
{
    internal class ConnectorEnumConverter<T> : JsonConverter<T> where T : struct, IEquatable<T>
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return (T)Activator.CreateInstance(typeof(T), reader.GetString());
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}
