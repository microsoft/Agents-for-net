﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder.Errors;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.UserAuth
{
    public abstract class OBOExchange
    {
        private readonly IConnections _connections;

        public OBOExchange(IConnections connections)
        {
            _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        }

        protected abstract OBOSettings GetOBOSettings();

        protected async Task<TokenResponse> HandleOBO(ITurnContext turnContext, TokenResponse token, string exchangeConnection = null, IList<string> exchangeScopes = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(token?.Token))
            {
                return null;
            }

            var oboSettings = GetOBOSettings();
            var connectionName = exchangeConnection ?? oboSettings.OBOConnectionName;
            IList<string> scopes = exchangeScopes ?? oboSettings.OBOScopes;

            // If OBO is not supplied (by config or passed) return token as-is.
            if (string.IsNullOrEmpty(connectionName) || scopes == null || !scopes.Any())
            {
                return token;
            }

            // Can we even exchange this?
            if (!token.IsExchangeable)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.OBONotExchangeableToken, null, [connectionName]);
            }

            // Can the named Connection even do this?
            if (!TryGetOBOProvider(connectionName, out var oboExchangeProvider))
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.OBONotSupported, null, [connectionName]);
            }

            // Do exchange.
            try
            {
                token = await oboExchangeProvider.AcquireTokenOnBehalfOf(scopes, token.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.OBOExchangeFailed, ex, [connectionName, string.Join(",", scopes)]);
            }

            if (token == null)
            {
                // AcquireTokenOnBehalfOf returned null
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.OBOExchangeFailed, null, [connectionName, string.Join(",", scopes)]);
            }

            return token ?? null;
        }

        protected bool TryGetOBOProvider(string connectionName, out IOBOExchange oboExchangeProvider)
        {
            if (_connections.TryGetConnection(connectionName, out var tokenProvider))
            {
                if (tokenProvider is IOBOExchange oboExchange)
                {
                    oboExchangeProvider = oboExchange;
                    return true;
                }
            }

            oboExchangeProvider = null;
            return false;
        }
    }
}