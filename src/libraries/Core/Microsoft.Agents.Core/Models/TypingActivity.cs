// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;

namespace Microsoft.Agents.Core.Models
{
    [ActivityType(ActivityTypes.Typing)]
    public class TypingActivity : Activity, ITypingActivity
    {
        public TypingActivity() : base(ActivityTypes.Typing)
        {
        }

        public string Text { get; set; }
        public string TextFormat { get; set; }
    }
}
