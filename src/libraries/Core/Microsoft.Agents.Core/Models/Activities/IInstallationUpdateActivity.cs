// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.Models.Activities
{
    /// <summary>
    /// An Agent was installed or removed from a channel.
    /// </summary>
    public interface IInstallationUpdateActivity : IActivity
    {
        /// <summary>
        /// The action field describes the meaning of the contact relation update Activity. The value of the Action field
        /// is a string. Only values of add and remove are defined, which denote a relationship between the users/Agents in 
        /// the from and recipient fields.
        /// </summary>
        string Action { get; set; }
    }
}
