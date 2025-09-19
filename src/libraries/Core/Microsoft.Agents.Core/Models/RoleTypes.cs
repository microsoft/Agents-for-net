// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.Models
{
    /// <summary>
    /// Defines values for RoleTypes.
    /// </summary>
    public static class RoleTypes
    {
        /// <summary>
        /// The type for user roles.
        /// </summary>
        public const string User = "user";

        /// <summary>
        /// The type for Agent roles.
        /// </summary>
        public const string Agent = "agent";

        /// <summary>
        /// The type for skill roles.
        /// </summary>
        public const string Skill = "skill";

        /// <summary>
        /// Agentic AI - AAI role
        /// </summary>
        public const string AgenticIdentity = "agenticAppInstance";

        /// <summary>
        /// Agentic AI - AAI role
        /// </summary>
        public const string AgenticUser = "agenticUser";
    }
}
