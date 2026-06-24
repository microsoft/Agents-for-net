// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.MSTeams
{
    public class TeamsTurnContext : TurnContextWrapper, ITeamsTurnContext
    {
        public TeamsTurnContext(ITurnContext turnContext) : base(turnContext)
        {
        }

        /// <inheritdoc/>
        public Microsoft.Teams.Api.Clients.ApiClient Client => _turnContext.Services.Get<Microsoft.Teams.Api.Clients.ApiClient>();

        /// <inheritdoc/>
        public Task<ResourceResponse> SendTargetedActivityAsync(IActivity activity, CancellationToken cancellationToken = default)
        {
            return SendActivityAsync(activity.Clone().MakeTargetedActivity(), cancellationToken);
        }

        /// <inheritdoc/>
        public Task<ResourceResponse[]> SendTargetedActivitiesAsync(IActivity[] activities, CancellationToken cancellationToken = default)
        {
            var clonedActivities = new List<IActivity>(activities.Length);
            foreach (var activity in activities)
            {
                clonedActivities.Add(activity.Clone().MakeTargetedActivity());
            }

            return SendActivitiesAsync([.. clonedActivities], cancellationToken);
        }
    }
}
