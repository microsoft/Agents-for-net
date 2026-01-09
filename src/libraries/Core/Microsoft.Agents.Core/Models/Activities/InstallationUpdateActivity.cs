// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.Models.Activities
{
    public class InstallationUpdateActivity : Activity, IInstallationUpdateActivity
    {
        public InstallationUpdateActivity(string action) : base(ActivityTypes.InstallationUpdate)
        {
            Action = action;
        }

        public string Action { get; set; }
    }
}
