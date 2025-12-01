// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using Microsoft;

namespace Microsoft.Agents.Core.Models.Cards
{
    /// <summary> Supplementary parameter for media events. </summary>
    public class MediaEventValue
    {
        public MediaEventValue() { }

        /// <summary> Initializes a new instance of MediaEventValue. </summary>
        public MediaEventValue(object cardValue = default)
        {
            CardValue = cardValue;
        }

        /// <summary> Callback parameter specified in the Value field of the MediaCard that originated this event. </summary>
        public object CardValue { get; }
    }
}
