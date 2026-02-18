// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Builder.App.Proactive
{
    /// <summary>
    /// Represents a record containing a conversation reference and associated claims for a conversation.
    /// </summary>
    /// <remarks>This class is typically used to persist or transfer conversation context and identity claims
    /// between different components or services in an agent application. The claims are extracted from a provided identity
    /// and can be used for authentication or authorization scenarios. The conversation reference provides the necessary
    /// information to resume or continue a conversation thread.</remarks>
    public class ConversationReferenceRecord
    {
        public ConversationReferenceRecord() { }

        /// <summary>
        /// Initializes a new instance of the ConversationReferenceRecord class using the specified turn context.
        /// </summary>
        /// <param name="context">The turn context containing the identity and activity information to initialize the conversation reference.
        /// Cannot be null.</param>
        public ConversationReferenceRecord(ITurnContext context) : this(context?.Identity, context?.Activity?.GetConversationReference()) { }

        /// <summary>
        /// Initializes a new instance of the ConversationReferenceRecord class using the specified identity and
        /// conversation reference.
        /// </summary>
        /// <param name="identity">The ClaimsIdentity containing the claims associated with the conversation. Cannot be null.</param>
        /// <param name="reference">The ConversationReference that identifies the conversation. Cannot be null.</param>
        public ConversationReferenceRecord(ClaimsIdentity identity, ConversationReference reference)
        {
            AssertionHelpers.ThrowIfNull(identity, nameof(identity));
            AssertionHelpers.ThrowIfNull(reference, nameof(reference));

            Claims = ClaimsFromIdentity(identity);
            Reference = reference;
        }

        /// <summary>
        /// Gets or sets the collection of claims associated with the current entity.
        /// </summary>
        /// <remarks>Each entry in the dictionary represents a claim, where the key is the claim type and
        /// the value is the claim value. The collection may be null if no claims are associated.</remarks>
        public IDictionary<string, string>? Claims { get; set; }

        /// <summary>
        /// Gets or sets the reference information for the conversation associated with this instance.
        /// </summary>
        public ConversationReference? Reference { get; set; }

        /// <summary>
        /// Gets a claims-based identity constructed from the current set of claims.
        /// </summary>
        /// <remarks>The returned identity reflects the current claims in the object. Modifying the claims
        /// after accessing this property does not update the returned identity instance.</remarks>
        [JsonIgnore]
        public ClaimsIdentity Identity => new(Claims?.Select(kv => new Claim(kv.Key, kv.Value)).ToList());

        /// <summary>
        /// Extracts a dictionary of selected claim types and their values from the specified identity.
        /// </summary>
        /// <remarks>Only claims with the types 'aud', 'azp', 'appid', 'idtyp', 'ver', and 'iss' are
        /// included in the returned dictionary. If multiple claims of the same type exist, only the last occurrence is
        /// included due to dictionary key uniqueness.</remarks>
        /// <param name="identity">The identity from which to extract claims. Cannot be null.</param>
        /// <returns>A dictionary containing the values of the 'aud', 'azp', 'appid', 'idtyp', 'ver', and 'iss' claims from the
        /// identity. The dictionary is empty if none of these claims are present.</returns>
        public static IDictionary<string, string> ClaimsFromIdentity(ClaimsIdentity identity)
        {
            return identity.Claims.Where(c =>
            {
                return c.Type == "aud"
                    || c.Type == "azp"
                    || c.Type == "appid"
                    || c.Type == "idtyp"
                    || c.Type == "ver"
                    || c.Type == "iss";
            }).ToDictionary(c => c.Type, c => c.Value);
        }
    }
}
