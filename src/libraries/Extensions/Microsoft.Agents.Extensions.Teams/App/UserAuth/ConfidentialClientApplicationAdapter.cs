// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensibility;
using System.Collections.Generic;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.App.UserAuth
{
    internal class ConfidentialClientApplicationAdapter : IConfidentialClientApplicationAdapter
    {
        private readonly IConfidentialClientApplication _msal;

        public ConfidentialClientApplicationAdapter(IConfidentialClientApplication msal)
        {
            _msal = msal;
        }

        public IAppConfig AppConfig
        {
            get
            {
                return _msal.AppConfig;
            }
        }

        public Task<AuthenticationResult> InitiateLongRunningProcessInWebApi(IEnumerable<string> scopes, string userToken, ref string longRunningProcessSessionKey)
        {
            return ((ILongRunningWebApi)_msal).InitiateLongRunningProcessInWebApi(
                                scopes,
                                userToken,
                                ref longRunningProcessSessionKey
                            ).ExecuteAsync();
        }

        public async Task<bool> StopLongRunningProcessInWebApiAsync(string longRunningProcessSessionKey, CancellationToken cancellationToken = default)
        {
            ILongRunningWebApi? oboCca = _msal as ILongRunningWebApi;
            if (oboCca != null)
            {
                return await oboCca.StopLongRunningProcessInWebApiAsync(longRunningProcessSessionKey, cancellationToken);
            }
            return false;
        }

        public async Task<AuthenticationResult> AcquireTokenInLongRunningProcess(IEnumerable<string> scopes, string longRunningProcessSessionKey)
        {
            return await ((ILongRunningWebApi)_msal).AcquireTokenInLongRunningProcess(
                        scopes,
                        longRunningProcessSessionKey
                    ).ExecuteAsync();
        }

        public async Task<TokenResponse> TryGetUserToken(ITurnContext context, string name, TeamsSsoSettings settings)
        {
            string homeAccountId = $"{context.Activity.From.AadObjectId}.{context.Activity.Conversation.TenantId}";
            try
            {
                AuthenticationResult result = await AcquireTokenInLongRunningProcess(settings.Scopes, homeAccountId);

                var tokenResponse = new TokenResponse()
                {
                    ChannelId = context.Activity.ChannelId,
                    ConnectionName = name,
                    Token = result.AccessToken,
                    Expiration = result.ExpiresOn
                };

                return tokenResponse;
            }
            catch (MsalClientException)
            {
                // Cannot acquire token from cache
            }

            return null; // Return empty indication no token found in cache
        }
    }
}
