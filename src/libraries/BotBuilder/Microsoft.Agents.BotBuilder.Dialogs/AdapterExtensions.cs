// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Microsoft.Agents.State;
using Microsoft.Agents.Core.Interfaces;
using Microsoft.Agents.Storage;

namespace Microsoft.Agents.BotBuilder.Dialogs
{
    /// <summary>
    /// Defines extension methods for the <see cref="IChannelAdapter"/> class.
    /// </summary>
    public static class AdapterExtensions
    {
        /// <summary>
        /// Adds middleware to the adapter to register an <see cref="IStorage"/> object on the turn context.
        /// The middleware registers the state objects on the turn context at the start of each turn.
        /// </summary>
        /// <param name="ChannelAdapter">The adapter on which to register the storage object.</param>
        /// <param name="storage">The storage object to register.</param>
        /// <returns>The updated adapter.</returns>
        /// <remarks>
        /// To get the storage object, use the turn context's <see cref="ITurnContext.TurnState"/>
        /// property's <see cref="TurnContextStateCollection.Get{T}()"/> method.
        /// </remarks>
        public static IChannelAdapter UseStorage(this IChannelAdapter ChannelAdapter, IStorage storage)
        {
            return ChannelAdapter.Use(new RegisterClassMiddleware<IStorage>(storage ?? throw new ArgumentNullException(nameof(storage))));
        }

        /// <summary>
        /// Adds middleware to the adapter to register one or more <see cref="BotState"/> objects on the turn context.
        /// The middleware registers the state objects on the turn context at the start of each turn.
        /// </summary>
        /// <param name="ChannelAdapter">The adapter on which to register the state objects.</param>
        /// <param name="botStates">The state objects to register.</param>
        /// <returns>The updated adapter.</returns>
        /// <remarks>
        /// To get the state objects, use the turn context's <see cref="ITurnContext.TurnState"/>
        /// property's <see cref="TurnContextStateCollection.Get{T}(string)"/> method, where the `key`
        /// parameter is the fully qualified name of the type of bot state to get.
        /// </remarks>
        public static IChannelAdapter UseBotState(this IChannelAdapter ChannelAdapter, params BotState[] botStates)
        {
            if (botStates == null)
            {
                throw new ArgumentNullException(nameof(botStates));
            }

            foreach (var botState in botStates)
            {
                ChannelAdapter.Use(new RegisterClassMiddleware<BotState>(botState, botState.GetType().FullName));
            }

            return ChannelAdapter;
        }
    }
}
