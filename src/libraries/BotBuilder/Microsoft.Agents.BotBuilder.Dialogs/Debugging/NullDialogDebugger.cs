﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.BotBuilder.Dialogs.Debugging
{
    /// <summary>
    /// Default Dialog Debugger which simply ignores step calls for the IDialogDebuggerinterface.
    /// </summary>
    public class NullDialogDebugger : IDialogDebugger
    {
        public static readonly IDialogDebugger Instance = new NullDialogDebugger();

        private NullDialogDebugger()
        {
        }

        Task IDialogDebugger.StepAsync(DialogContext context, object item, string more, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
