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
    /// See <see cref="RecordBuilder"/> to ease creation of an instance of this class.</remarks>
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
        /// <remarks>This is the list of JWT claims.  For Azure Bot Service, only the 'aud' claim is required.  The 'aud' claim 
        /// should be the ClientId of the Azure Bot.</remarks>
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
    }
}
