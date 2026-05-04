// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;

namespace Microsoft.Agents.Core.Models
{
    [ActivityType(ActivityTypes.InstallationUpdate)]
    public class InstallationUpdateActivity : Activity, IInstallationUpdateActivity
    {
        public InstallationUpdateActivity(string action) : base(ActivityTypes.InstallationUpdate)
        {
            Action = action;
        }

        public string Action { get; set; }
    }
}
