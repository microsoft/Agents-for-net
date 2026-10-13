// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using System;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore
{
    /// <summary>
    /// Handles exceptions that escape agent turn processing in a
    /// <see cref="Microsoft.Agents.Hosting.AspNetCore.CloudAdapter"/>.
    /// </summary>
    /// <remarks>
    /// Register an implementation with dependency injection to replace the default
    /// <see cref="Microsoft.Agents.Builder.IChannelAdapter.OnTurnError"/> handler without deriving from
    /// <see cref="Microsoft.Agents.Hosting.AspNetCore.CloudAdapter"/>. A replacement is responsible for both logging
    /// the exception and sending any desired response activities.
    /// </remarks>
    public interface ICloudAdapterErrorHandler
    {
        /// <summary>
        /// Handles an exception that escaped agent turn processing.
        /// </summary>
        /// <param name="turnContext">The context for the turn that failed.</param>
        /// <param name="exception">The unhandled exception.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task HandleTurnErrorAsync(ITurnContext turnContext, Exception exception);
    }
}
