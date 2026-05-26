// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.State;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.Messages;

public delegate Task TeamsRouteHandler(TeamsTurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken);
