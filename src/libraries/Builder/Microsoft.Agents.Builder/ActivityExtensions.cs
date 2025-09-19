// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;


namespace Microsoft.Agents.Builder
{
    public static class ActivityExtensions
    {
        public static bool IsAgenticRequest(this IActivity activity)
        {
            AssertionHelpers.ThrowIfNull(activity, nameof(activity));
            return AgenticAuthorization.IsAgenticRequest(activity);
        }
    }
}
