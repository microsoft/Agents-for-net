﻿using Microsoft.Agents.Core.Interfaces;
using Microsoft.Agents.Core.Teams.Models;
using Microsoft.Teams.AI.State;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Teams.AI
{
    /// <summary>
    /// Function for handling read receipt events.
    /// </summary>
    /// <typeparam name="TState">Type of the turn state. This allows for strongly typed access to the turn state.</typeparam>
    /// <param name="turnContext">A strongly-typed context object for this turn.</param>
    /// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
    /// <param name="data">The read receipt data.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns></returns>
    public delegate Task ReadReceiptHandler<TState>(ITurnContext turnContext, TState turnState, ReadReceiptInfo data, CancellationToken cancellationToken) where TState : TurnState;
}
