// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.State;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.FileConsents;

/// <summary>
/// Function for handling file consent card activities.
/// </summary>
/// <param name="turnContext">The context for the current conversation turn.</param>
/// <param name="turnState">The state object that stores arbitrary data for this turn.</param>
/// <param name="fileConsentCardResponse">The response representing the value of the invoke activity sent when the user acts on a file consent card.</param>
/// <param name="cancellationToken">A cancellation token that can be used by other objects
/// or threads to receive notice of cancellation.</param>
/// <returns>A task that represents the work queued to execute.</returns>
public delegate Task FileConsentHandler(ITurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.FileConsentCardResponse fileConsentCardResponse, CancellationToken cancellationToken);
