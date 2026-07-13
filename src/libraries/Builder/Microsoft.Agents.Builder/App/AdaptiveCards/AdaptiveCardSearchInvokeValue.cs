// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System;

namespace Microsoft.Agents.Builder.App.AdaptiveCards
{
    [Obsolete("Use Microsoft.Agents.Core.Models.AdaptiveCardSearchInvokeValue instead.")]
    public class AdaptiveCardSearchInvokeValue : SearchInvokeValue
    {
        public string? Dataset { get; set; }
    }
}
