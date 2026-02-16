// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Builder.App.Proactive
{
    public class ConversationReferenceRecord
    {
        public static string GetKey(string conversationId)
        {
            return $"conversationreferences/{conversationId}";
        }

        public ConversationReferenceRecord() { }

        public ConversationReferenceRecord(ClaimsIdentity identity, ConversationReference reference)
        {
            Claims = identity.Claims.Where(c =>
            {
                return c.Type == "aud" 
                    || c.Type == "azp"
                    || c.Type == "appid"
                    || c.Type == "idtyp"
                    || c.Type == "ver";
            }).ToDictionary(c => c.Type, c => c.Value);
            Reference = reference;
        }

        public IDictionary<string, string>? Claims { get; set; }
        public ConversationReference? Reference { get; set; }

        [JsonIgnore]
        public ClaimsIdentity Identity => new(Claims?.Select(kv => new Claim(kv.Key, kv.Value)).ToList());
    }
}
