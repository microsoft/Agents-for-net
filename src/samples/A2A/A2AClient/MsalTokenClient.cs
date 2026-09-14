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
    private A2AAgentCardAuthentication? _delegatedAuthentication;
    private A2AAgentCardAuthentication? _applicationAuthentication;

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

    public void Configure(A2AAgentCardAuthentication authentication)
    {
        ArgumentNullException.ThrowIfNull(authentication);

        switch (authentication.Mode)
        {
            case A2AAuthMode.Delegated:
                _delegatedAuthentication = authentication;
                _publicClientApplication = null;
                break;
            case A2AAuthMode.App:
                _applicationAuthentication = authentication;
                _confidentialClientApplication = null;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(authentication));
        }
    }

    public async Task<string> AcquireDelegatedTokenAsync(CancellationToken cancellationToken)
    {
        ValidateRequiredConfiguration(
            (nameof(A2AClientAuthenticationOptions.TenantId), _options.TenantId),
            (nameof(A2AClientAuthenticationOptions.PublicClientId), _options.PublicClientId));
        A2AAgentCardAuthentication authentication = GetConfiguredAuthentication(A2AAuthMode.Delegated);

        if (_delegatedTokenFactory is not null)
        {
            return await _delegatedTokenFactory(_options, cancellationToken).ConfigureAwait(false);
        }

        IPublicClientApplication publicClient = GetOrCreatePublicClientApplication();
        string[] scopes = authentication.Scopes.ToArray();
        IAccount? account = (await publicClient.GetAccountsAsync().ConfigureAwait(false)).FirstOrDefault();

        if (account is not null)
        {
            try
            {
                AuthenticationResult silentResult = await publicClient
                    .AcquireTokenSilent(scopes, account)
                    .WithTenantId(_options.TenantId!)
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
            .WithTenantId(_options.TenantId!)
            .ExecuteAsync(cancellationToken)
            .ConfigureAwait(false);

        return deviceCodeResult.AccessToken;
    }

    public async Task<string> AcquireApplicationTokenAsync(CancellationToken cancellationToken)
    {
        ValidateRequiredConfiguration(
            (nameof(A2AClientAuthenticationOptions.TenantId), _options.TenantId),
            (nameof(A2AClientAuthenticationOptions.ConfidentialClientId), _options.ConfidentialClientId),
            (nameof(A2AClientAuthenticationOptions.ConfidentialClientSecret), _options.ConfidentialClientSecret));
        A2AAgentCardAuthentication authentication = GetConfiguredAuthentication(A2AAuthMode.App);

        if (_applicationTokenFactory is not null)
        {
            return await _applicationTokenFactory(_options, cancellationToken).ConfigureAwait(false);
        }

        IConfidentialClientApplication confidentialClient = GetOrCreateConfidentialClientApplication();

        AuthenticationResult result = await confidentialClient
            .AcquireTokenForClient(authentication.Scopes.ToArray())
            .WithTenantId(_options.TenantId!)
            .ExecuteAsync(cancellationToken)
            .ConfigureAwait(false);

        return result.AccessToken;
    }

    private IPublicClientApplication GetOrCreatePublicClientApplication()
    {
        _publicClientApplication ??= PublicClientApplicationBuilder.Create(_options.PublicClientId!)
            .WithAuthority(GetAuthority(_delegatedAuthentication!.TokenUrl), validateAuthority: true)
            .WithDefaultRedirectUri()
            .Build();

        return _publicClientApplication;
    }

    private IConfidentialClientApplication GetOrCreateConfidentialClientApplication()
    {
        _confidentialClientApplication ??= ConfidentialClientApplicationBuilder.Create(_options.ConfidentialClientId!)
            .WithAuthority(GetAuthority(_applicationAuthentication!.TokenUrl), validateAuthority: true)
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

    private A2AAgentCardAuthentication GetConfiguredAuthentication(A2AAuthMode mode)
    {
        A2AAgentCardAuthentication? authentication = mode == A2AAuthMode.Delegated
            ? _delegatedAuthentication
            : _applicationAuthentication;
        return authentication ?? throw new InvalidOperationException(
            $"The Agent Card has not configured a {mode} OAuth flow.");
    }

    private static Uri GetAuthority(string tokenUrl)
    {
        var endpoint = new Uri(tokenUrl, UriKind.Absolute);
        string path = endpoint.AbsolutePath;
        const string OAuthPath = "/oauth2/";
        int oauthPathIndex = path.IndexOf(OAuthPath, StringComparison.OrdinalIgnoreCase);
        if (oauthPathIndex <= 1)
        {
            throw new InvalidOperationException(
                $"The Agent Card token endpoint '{endpoint.GetLeftPart(UriPartial.Authority)}' is not a supported Microsoft Entra token endpoint.");
        }

        return new UriBuilder(endpoint.Scheme, endpoint.Host, endpoint.Port, path[..oauthPathIndex]).Uri;
    }
}
