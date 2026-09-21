// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.MSTeams.Models;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.Agents.Extensions.MSTeams;

internal static class PromptPreviewActivityNormalizer
{
    private static readonly Regex QuotedPlaceholderRegex = new(
        "<quoted messageId=\"[^\"]*\"/>",
        RegexOptions.Compiled);

    public static void Apply(IActivity activity, string messageId)
    {
        ArgumentNullException.ThrowIfNull(activity);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

        if (activity.Entities != null)
        {
            for (var index = activity.Entities.Count - 1; index >= 0; index--)
            {
                if (activity.Entities[index].Type == QuotedReplyEntity.EntityName)
                {
                    activity.Entities.RemoveAt(index);
                }
            }
        }

        if (activity.Text != null)
        {
            var textWithoutPlaceholder = QuotedPlaceholderRegex.Replace(activity.Text, string.Empty);
            if (textWithoutPlaceholder.Length != activity.Text.Length)
            {
                activity.Text = textWithoutPlaceholder.Trim();
            }
        }

        if (activity.Entities?.Any(entity => entity.Type == TargetedMessageInfoEntity.EntityName) != true)
        {
            activity.Entities ??= [];
            activity.Entities.Add(new TargetedMessageInfoEntity { MessageId = messageId });
        }
    }
}
