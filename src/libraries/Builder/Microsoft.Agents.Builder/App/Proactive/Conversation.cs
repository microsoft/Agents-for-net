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
    /// information to resume or continue a conversation thread.<br/><br/>
    /// See <see cref="ConversationBuilder"/> to ease creation of an instance of this class.</remarks>
    public class Conversation
    {
        public Conversation() { }

        /// <summary>
        /// Initializes a new instance of the Conversation class using the specified turn context.
        /// </summary>
        /// <param name="context">The turn context containing the identity and activity information to initialize the conversation reference.
        /// Cannot be null.</param>
        public Conversation(ITurnContext context) : this(context?.Identity, context?.Activity?.GetConversationReference()) { }

        /// <summary>
        /// Initializes a new instance of the Conversation class using the specified identity and
        /// conversation reference.
        /// </summary>
        /// <param name="identity">The ClaimsIdentity containing the claims associated with the conversation. Cannot be null.</param>
        /// <param name="reference">The ConversationReference that identifies the conversation. Cannot be null.</param>
        public Conversation(ClaimsIdentity identity, ConversationReference reference)
        {
            AssertionHelpers.ThrowIfNull(identity, nameof(identity));
            AssertionHelpers.ThrowIfNull(reference, nameof(reference));

            Claims = ClaimsFromIdentity(identity);
            Reference = reference;
        }

        /// <summary>
        /// Initializes a new instance of the Conversation class with the specified claims and conversation reference.
        /// </summary>
        /// <param name="claims">A dictionary containing claim types and their corresponding values associated with the conversation. Cannot
        /// be null.</param>
        /// <param name="reference">The reference information that uniquely identifies the conversation. Cannot be null.</param>
        public Conversation(IDictionary<string, string> claims, ConversationReference reference)
        {
            AssertionHelpers.ThrowIfNull(claims, nameof(claims));
            AssertionHelpers.ThrowIfNull(reference, nameof(reference));

            Claims = claims;
            Reference = reference;
        }

        /// <summary>
        /// Gets or sets the collection of claims associated with the current entity.
        /// </summary>
        /// <remarks>This is the list of JWT claims.  For Azure Bot Service, only the 'aud' claim is required.  The 'aud' claim 
        /// should be the ClientId of the Azure Bot.</remarks>
        internal IDictionary<string, string>? Claims { get; set; }

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
        public ClaimsIdentity Identity => IdentityFromClaims(Claims);

        /// <summary>
        /// Extracts a dictionary of selected claim types and their values from the specified identity.
        /// </summary>
        /// <remarks>Only claims with the types 'aud', 'azp', 'appid', 'idtyp', 'ver', 'iss', and 'tid' are
        /// included in the returned dictionary.</remarks>
        /// <param name="identity">The identity from which to extract claims. Cannot be null.</param>
        /// <returns>A dictionary containing applicable claims from the ClaimsIdentity.</returns>
        public static IDictionary<string, string> ClaimsFromIdentity(ClaimsIdentity identity)
        {
            if (identity == null)
            {
                return new Dictionary<string, string>();
            }

            return identity.Claims.Where(c =>
            {
                return c.Type == "aud"
                    || c.Type == "azp"
                    || c.Type == "appid"
                    || c.Type == "idtyp"
                    || c.Type == "ver"
                    || c.Type == "iss"
                    || c.Type == "tid";
            }).ToDictionary(c => c.Type, c => c.Value);
        }

        /// <summary>
        /// Creates a new ClaimsIdentity from the specified collection of claim type and value pairs.
        /// </summary>
        /// <param name="claims">A dictionary containing claim types as keys and their corresponding claim values. Cannot be null.</param>
        /// <returns>A ClaimsIdentity containing a claim for each entry in the provided dictionary.</returns>
        public static ClaimsIdentity IdentityFromClaims(IDictionary<string, string> claims)
        {
            if (claims == null)
            {
                return new ClaimsIdentity();
            }

            return new ClaimsIdentity([.. claims.Select(kv => new Claim(kv.Key, kv.Value))]);
        }

        /// <summary>
        /// Determines whether the current object is valid based on the presence of a reference and an audience claim.
        /// </summary>
        /// <remarks>Use this method to verify that the object has been properly initialized and contains
        /// the required audience claim before performing operations that depend on these values.</remarks>
        /// <returns>true if the reference is not null and the claims dictionary contains an "aud" (audience) entry; otherwise,
        /// false.</returns>
        public bool IsValid()
        {
            return Reference != null && (bool)Claims?.TryGetValue("aud", out _);
        }
    }
}
