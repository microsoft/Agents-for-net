// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.Models
{
    public class SubChannelInfo :Entity
    {
        public SubChannelInfo() : base(EntityTypes.SubChannelInfo)
        {
        }

        public string ChannelId { get; set; }
    }
}
