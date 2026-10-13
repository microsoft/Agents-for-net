// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder.UserAuth;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.Tests;

/// <summary>
/// External user authorization handler used to verify that non-Builder handlers require an
/// explicit assembly name.
/// </summary>
public sealed class ExternalUserAuthorization : IUserAuthorization
{
    public ExternalUserAuthorization(string name, IStorage storage, IConnections connections, IConfigurationSection configurationSection, ILogger logger = null)
    {
        Name = name;
    }

    public string Name { get; }

    public Task<TokenResponse> GetRefreshedUserTokenAsync(ITurnContext turnContext, string exchangeConnection = null, IList<string> exchangeScopes = null, CancellationToken cancellationToken = default)
        => Task.FromResult<TokenResponse>(null);

    public Task ResetStateAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<TokenResponse> SignInUserAsync(ITurnContext turnContext, bool forceSignIn = false, string exchangeConnection = null, IList<string> exchangeScopes = null, CancellationToken cancellationToken = default)
        => Task.FromResult<TokenResponse>(null);

    public Task SignOutUserAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
