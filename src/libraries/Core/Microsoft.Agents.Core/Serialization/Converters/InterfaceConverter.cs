// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace Microsoft.Agents.Core.Serialization.Converters
{
    public class InterfaceConverter<TM, TI> : JsonConverter<TI> where TM : class, TI
    {
        public override TI Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return JsonSerializer.Deserialize<TM>(ref reader, options);
        }

        public override void Write(Utf8JsonWriter writer, TI value, JsonSerializerOptions options) 
        { 
            JsonSerializer.Serialize(writer, (TM) value, options);
        }
    }
}
