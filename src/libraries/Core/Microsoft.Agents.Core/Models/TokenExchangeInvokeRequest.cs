// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Collections.Generic;
using System.Text.Json;

namespace Microsoft.Agents.Core.Models
{
    /// <summary> A request to exchange a token. </summary>
    public class TokenExchangeInvokeRequest
    {
        /// <summary> Initializes a new instance of TokenExchangeInvokeRequest. </summary>
        public TokenExchangeInvokeRequest()
        {
        }

        /// <summary>
        /// Gets or sets the id from the OAuthCard.
        /// </summary>
        /// <value>
        /// The id from the OAuthCard.
        /// </value>
        public string Id { get; set; }

        /// <summary> The connection name. </summary>
        public string ConnectionName { get; set; }
        /// <summary> The user token that can be exchanged. </summary>
        public string Token { get; set; }

        /// <summary>
        /// Gets properties that are not otherwise defined by the <see cref="Activity"/> type but that
        /// might appear in the serialized REST JSON object.
        /// </summary>
        /// <value>The extended properties for the object.</value>
        /// <remarks>With this, properties not represented in the defined type are not dropped when
        /// the JSON object is deserialized, but are instead stored in this property. Such properties
        /// will be written to a JSON object when the instance is serialized.</remarks>
        public IDictionary<string, JsonElement> Properties { get; set; } = new Dictionary<string, JsonElement>();
    }
}
