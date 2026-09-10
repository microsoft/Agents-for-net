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
    private readonly Func<A2AClientAuthenticationOptions, CancellationToken, Task<string>>? _delegatedTokenFactory;
    private readonly Func<A2AClientAuthenticationOptions, CancellationToken, Task<string>>? _applicationTokenFactory;
    private IPublicClientApplication? _publicClientApplication;
    private IConfidentialClientApplication? _confidentialClientApplication;

    public MsalTokenClient(A2AClientAuthenticationOptions options)
        : this(options, delegatedTokenFactory: null, applicationTokenFactory: null)
    {
    }

    internal MsalTokenClient(
        A2AClientAuthenticationOptions options,
        Func<A2AClientAuthenticationOptions, CancellationToken, Task<string>>? delegatedTokenFactory,
        Func<A2AClientAuthenticationOptions, CancellationToken, Task<string>>? applicationTokenFactory)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _delegatedTokenFactory = delegatedTokenFactory;
        _applicationTokenFactory = applicationTokenFactory;
    }

    public async Task<string> AcquireDelegatedTokenAsync(CancellationToken cancellationToken)
    {
        ValidateRequiredConfiguration(
            (nameof(A2AClientAuthenticationOptions.TenantId), _options.TenantId),
            (nameof(A2AClientAuthenticationOptions.PublicClientId), _options.PublicClientId),
            (nameof(A2AClientAuthenticationOptions.AgentDelegatedScope), _options.AgentDelegatedScope));

        if (_delegatedTokenFactory is not null)
        {
            return await _delegatedTokenFactory(_options, cancellationToken).ConfigureAwait(false);
        }

        IPublicClientApplication publicClient = GetOrCreatePublicClientApplication();
        string[] scopes = [_options.AgentDelegatedScope!];
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
        ValidateRequiredConfiguration(
            (nameof(A2AClientAuthenticationOptions.TenantId), _options.TenantId),
            (nameof(A2AClientAuthenticationOptions.ConfidentialClientId), _options.ConfidentialClientId),
            (nameof(A2AClientAuthenticationOptions.ConfidentialClientSecret), _options.ConfidentialClientSecret),
            (nameof(A2AClientAuthenticationOptions.AgentApplicationScope), _options.AgentApplicationScope));

        if (_applicationTokenFactory is not null)
        {
            return await _applicationTokenFactory(_options, cancellationToken).ConfigureAwait(false);
        }

        IConfidentialClientApplication confidentialClient = GetOrCreateConfidentialClientApplication();

        AuthenticationResult result = await confidentialClient
            .AcquireTokenForClient([_options.AgentApplicationScope!])
            .ExecuteAsync(cancellationToken)
            .ConfigureAwait(false);

        return result.AccessToken;
    }

    private IPublicClientApplication GetOrCreatePublicClientApplication()
    {
        _publicClientApplication ??= PublicClientApplicationBuilder.Create(_options.PublicClientId!)
            .WithAuthority(AzureCloudInstance.AzurePublic, _options.TenantId!)
            .WithDefaultRedirectUri()
            .Build();

        return _publicClientApplication;
    }

    private IConfidentialClientApplication GetOrCreateConfidentialClientApplication()
    {
        _confidentialClientApplication ??= ConfidentialClientApplicationBuilder.Create(_options.ConfidentialClientId!)
            .WithAuthority(AzureCloudInstance.AzurePublic, _options.TenantId!)
            .WithClientSecret(_options.ConfidentialClientSecret!)
            .Build();

        return _confidentialClientApplication;
    }

    private static void ValidateRequiredConfiguration(params (string Key, string? Value)[] requiredSettings)
    {
        string[] missingKeys = requiredSettings
            .Where(setting => string.IsNullOrWhiteSpace(setting.Value))
            .Select(setting => setting.Key)
            .ToArray();

        if (missingKeys.Length == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Missing required A2A client authentication configuration value(s): {string.Join(", ", missingKeys)}.");
    }
}
