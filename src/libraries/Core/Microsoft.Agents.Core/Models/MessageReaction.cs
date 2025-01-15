// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

namespace Microsoft.Agents.Core.Models
{
    /// <summary> Message reaction object. </summary>
    public class MessageReaction
    {
        public MessageReaction() { }

        /// <summary> Initializes a new instance of MessageReaction. </summary>
        /// <param name="type"> Message reaction types. </param>
        public MessageReaction(string type = default)
        {
            Type = type;
        }

        /// <summary> Message reaction types. </summary>
        public string Type { get; set; }
    }
}
