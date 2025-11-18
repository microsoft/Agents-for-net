// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App.UserAuth;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App
{
    /// <summary>
    /// AgentApplication ITurnContext extensions
    /// </summary>
    public static class TurnContextExtensions
    {
        public static IDictionary<string, string> GetTurnTokens(this TurnContext turnContext)
        {
            var userAuth = turnContext.Services.Get<UserAuthorization>();
            if (userAuth == null)
            {
                return new Dictionary<string, string>();
            }
            return userAuth.GetTurnTokens();
        }

        public static Task<string> GetTurnTokenAsyc(this TurnContext turnContext, string handlerName = null, CancellationToken cancellationToken = default)
        {
            var userAuth = turnContext.Services.Get<UserAuthorization>();
            if (userAuth == null)
            {
                return null;
            }

            return userAuth.GetTurnTokenAsync(turnContext, handlerName, cancellationToken);
        }

        public static Task<string> ExchangeTurnTokenAsync(this ITurnContext turnContext, string handlerName = default, string exchangeConnection = default, IList<string> exchangeScopes = default, CancellationToken cancellationToken = default)
        {
            var userAuth = turnContext.Services.Get<UserAuthorization>();
            if (userAuth == null)
            {
                return null;
            }

            return userAuth.ExchangeTurnTokenAsync(turnContext, handlerName, exchangeConnection, exchangeScopes, cancellationToken);
        }
    }
}
