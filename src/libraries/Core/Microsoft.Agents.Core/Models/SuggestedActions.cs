// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Agents.Core.Models
{
    /// <summary> SuggestedActions that can be performed. </summary>
    public class SuggestedActions
    {
        public SuggestedActions()
        {
            To = [];
            Actions = [];
        }

        /// <summary> Initializes a new instance of SuggestedActions. </summary>
        /// <param name="to"> Ids of the recipients that the actions should be shown to.  These Ids are relative to the channelId and a subset of all recipients of the activity. </param>
        /// <param name="actions"> Actions that can be shown to the user. </param>
        public SuggestedActions(IList<string> to = default, IList<CardAction> actions = default)
        {
            To = to ?? [];
            Actions = actions ?? [];
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Core.Models.SuggestedActions"/> class.
        /// </summary>
        /// <param name="to">Ids of the recipients that the actions should be
        /// shown to. These Ids are relative to the channelId and a subset of
        /// all recipients of the activity.</param>
        /// <param name="actions">Actions that can be shown to the user.</param>
        /// <exception cref="System.ArgumentNullException">ArgumentNullException.</exception>
        public SuggestedActions(IEnumerable<string> to, IEnumerable<CardAction> actions)
            : this([.. to], [.. actions])
        {
        }

        /// <summary> Ids of the recipients that the actions should be shown to.  These Ids are relative to the channelId and a subset of all recipients of the activity. </summary>
        public IList<string> To { get; set; }
        /// <summary> Actions that can be shown to the user. </summary>
        public IList<CardAction> Actions { get; set; }

        /// <summary>
        /// Adds a single action to <see cref="Microsoft.Agents.Core.Models.SuggestedActions.Actions"/> and returns this instance.
        /// </summary>
        public SuggestedActions AddAction(CardAction action)
        {
            Actions ??= [];
            Actions.Add(action);
            return this;
        }

        /// <summary>
        /// Adds one or more actions to <see cref="Microsoft.Agents.Core.Models.SuggestedActions.Actions"/> and returns this instance.
        /// </summary>
        public SuggestedActions AddActions(params CardAction[] actions)
        {
            if (actions == null)
            {
                return this;
            }

            Actions ??= [];
            foreach (var action in actions)
            {
                Actions.Add(action);
            }

            return this;
        }

        /// <summary>
        /// Adds one or more recipient ids to <see cref="Microsoft.Agents.Core.Models.SuggestedActions.To"/> and returns this instance.
        /// </summary>
        public SuggestedActions AddRecipients(params string[] recipients)
        {
            if (recipients == null)
            {
                return this;
            }

            To ??= [];
            foreach (var recipient in recipients)
            {
                To.Add(recipient);
            }

            return this;
        }
    }
}
