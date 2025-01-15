// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Collections.Generic;
using System.Text.Json;

namespace Microsoft.Agents.Core.Models
{
    /// <summary> Channel account information needed to route a message. </summary>
    public class ChannelAccount
    {
        /// <summary> Initializes a new instance of ChannelAccount. </summary>
        public ChannelAccount()
        {
        }

        /// <summary> Initializes a new instance of ChannelAccount. </summary>
        /// <param name="id"> Channel id for the user or bot on this channel (Example: joe@smith.com, or @joesmith or 123456). </param>
        /// <param name="name"> Display friendly name. </param>
        /// <param name="aadObjectId"> This account's object ID within Azure Active Directory (AAD). </param>
        /// <param name="role"> Role of the entity behind the account (Example: User, Bot, etc.). </param>
        public ChannelAccount(string id = default, string name = default, string role = default, string aadObjectId = default)
        {
            Id = id;
            Name = name;
            AadObjectId = aadObjectId;
            Role = role;
        }

        /// <summary> Channel id for the user or bot on this channel (Example: joe@smith.com, or @joesmith or 123456). </summary>
        public string Id { get; set; }
        /// <summary> Display friendly name. </summary>
        public string Name { get; set; }
        /// <summary> This account's object ID within Azure Active Directory (AAD). </summary>
        public string AadObjectId { get; set; }
        /// <summary> Role of the entity behind the account (Example: User, Bot, etc.). </summary>
        public string Role { get; set; }

        /// <summary>
        /// Gets properties that are not otherwise defined by the <see cref="ChannelAccount"/> type but that
        /// might appear in the REST JSON object.
        /// </summary>
        /// <value>The extended properties for the object.</value>
        /// <remarks>With this, properties not represented in the defined type are not dropped when
        /// the JSON object is deserialized, but are instead stored in this property. Such properties
        /// will be written to a JSON object when the instance is serialized.</remarks>
        public IDictionary<string, JsonElement> Properties { get; set; } = new Dictionary<string, JsonElement>();
    }
}
