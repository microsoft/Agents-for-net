// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.State;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams;

public delegate Task TeamsRouteHandler(TeamsTurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken);
