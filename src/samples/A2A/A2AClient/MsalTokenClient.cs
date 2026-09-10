// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;

namespace Microsoft.Agents.Samples.A2AClient;

internal sealed class MsalTokenClient
    : IMsalTokenClient
{
    private readonly A2AClientAuthenticationOptions _options;
    private IPublicClientApplication? _publicClientApplication;
    private IConfidentialClientApplication? _confidentialClientApplication;

    public MsalTokenClient(A2AClientAuthenticationOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<string> AcquireDelegatedTokenAsync(CancellationToken cancellationToken)
    {
        _options.Validate(A2AAuthMode.Delegated);
        IPublicClientApplication publicClient = GetOrCreatePublicClientApplication();
        string[] scopes = [_options.AgentDelegatedScope];
        IAccount? account = (await publicClient.GetAccountsAsync().ConfigureAwait(false)).FirstOrDefault();

        if (account is not null)
        {
            try
            {
                AuthenticationResult silentResult = await publicClient
                    .AcquireTokenSilent(scopes, account)
                    .ExecuteAsync(cancellationToken)
                    .ConfigureAwait(false);

                return silentResult.AccessToken;
            }
            catch (MsalUiRequiredException)
            {
            }
        }

        AuthenticationResult deviceCodeResult = await publicClient
            .AcquireTokenWithDeviceCode(
                scopes,
                callback =>
                {
                    Console.WriteLine(callback.Message);
                    return Task.CompletedTask;
                })
            .ExecuteAsync(cancellationToken)
            .ConfigureAwait(false);

        return deviceCodeResult.AccessToken;
    }

    public async Task<string> AcquireApplicationTokenAsync(CancellationToken cancellationToken)
    {
        _options.Validate(A2AAuthMode.App);
        IConfidentialClientApplication confidentialClient = GetOrCreateConfidentialClientApplication();

        AuthenticationResult result = await confidentialClient
            .AcquireTokenForClient([_options.AgentApplicationScope])
            .ExecuteAsync(cancellationToken)
            .ConfigureAwait(false);

        return result.AccessToken;
    }

    private IPublicClientApplication GetOrCreatePublicClientApplication()
    {
        _publicClientApplication ??= PublicClientApplicationBuilder.Create(_options.PublicClientId)
            .WithAuthority(AzureCloudInstance.AzurePublic, _options.TenantId)
            .WithDefaultRedirectUri()
            .Build();

        return _publicClientApplication;
    }

    private IConfidentialClientApplication GetOrCreateConfidentialClientApplication()
    {
        _confidentialClientApplication ??= ConfidentialClientApplicationBuilder.Create(_options.ConfidentialClientId)
            .WithAuthority(AzureCloudInstance.AzurePublic, _options.TenantId)
            .WithClientSecret(_options.ConfidentialClientSecret)
            .Build();

        return _confidentialClientApplication;
    }
}
