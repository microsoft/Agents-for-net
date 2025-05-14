// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;

namespace HeaderPropagation;

public static class ConversationStateExtensions
{
    public static void SaveActivity(this ConversationState state, IActivity activity)
    {
        state.SetValue($"history/{activity.Id}", activity);
    }

    public static IActivity GetActivity(this ConversationState state, string activityId)
    {
        return state.GetValue<IActivity>($"history/{activityId}");
    }
}
