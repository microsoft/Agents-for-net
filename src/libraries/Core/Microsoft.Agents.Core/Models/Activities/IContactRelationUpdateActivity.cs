// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.


namespace Microsoft.Agents.Core.Models.Activities
{
    /// <summary>
    /// A user has added an Agent to their contact list, removed the Agent from their contact list, or otherwise changed the relationship between user and Agent.
    /// </summary>
    public interface IContactRelationUpdateActivity : IActivity
    {
        /// <summary>
        /// The action field describes the meaning of the contact relation update Activity. The value of the Action field
        /// is a string. Only values of add and remove are defined, which denote a relationship between the users/Agents in 
        /// the from and recipient fields.
        /// </summary>
        string Action { get; set; }
    }
}
