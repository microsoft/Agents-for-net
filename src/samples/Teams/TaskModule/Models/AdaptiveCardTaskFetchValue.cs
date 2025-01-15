// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace TaskModule.Models
{
    public class AdaptiveCardTaskFetchValue<T>
    {
        [JsonPropertyName("msteams")]
        public object Type { get; set; } = ("{\"type\": \"task/fetch\" }");

        [JsonPropertyName("data")]
        public T Data { get; set; }
    }
}
