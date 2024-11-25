﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder;

namespace Microsoft.Agents.BotBuilder.Dialogs.Memory.Scopes
{
    /// <summary>
    /// UserMemoryScope represents User scoped memory.
    /// </summary>
    /// <remarks>This relies on the UserState object being accessible from turnContext.TurnState.Get&lt;UserState&gt;().</remarks>
    public class UserMemoryScope : BotStateMemoryScope<UserState>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UserMemoryScope"/> class.
        /// </summary>
        public UserMemoryScope()
            : base(ScopePath.User)
        {
        }
    }
}
