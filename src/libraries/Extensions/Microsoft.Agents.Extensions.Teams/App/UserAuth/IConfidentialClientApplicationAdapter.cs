// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.Core.Models;
using Microsoft.Identity.Client;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.App.UserAuth
{
    internal interface IConfidentialClientApplicationAdapter
    {
        IAppConfig AppConfig { get; }

        Task<AuthenticationResult> InitiateLongRunningProcessInWebApi(IEnumerable<string> scopes, string userToken, ref string longRunningProcessSessionKey);

        Task<bool> StopLongRunningProcessInWebApiAsync(string longRunningProcessSessionKey, CancellationToken cancellationToken = default);

        Task<AuthenticationResult> AcquireTokenInLongRunningProcess(IEnumerable<string> scopes, string longRunningProcessSessionKey);

        Task<TokenResponse> TryGetUserToken(ITurnContext context, string name, TeamsSsoSettings settings);
    }
}
