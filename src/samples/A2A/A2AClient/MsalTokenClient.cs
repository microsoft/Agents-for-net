// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;

namespace Microsoft.Agents.Samples.A2AClient;

internal sealed class MsalTokenClient
    : IMsalTokenClient
{
    private readonly A2AClientAuthenticationOptions _options;
    private readonly Func<A2AClientAuthenticationOptions, MsalTokenAcquisitionRequest, CancellationToken, Task<string>>? _delegatedTokenFactory;
    private readonly Func<A2AClientAuthenticationOptions, MsalTokenAcquisitionRequest, CancellationToken, Task<string>>? _applicationTokenFactory;
    private IPublicClientApplication? _publicClientApplication;
    private IConfidentialClientApplication? _confidentialClientApplication;
    private MsalTokenAcquisitionRequest? _delegatedAcquisitionRequest;
    private MsalTokenAcquisitionRequest? _applicationAcquisitionRequest;

    public MsalTokenClient(A2AClientAuthenticationOptions options)
        : this(options, delegatedTokenFactory: null, applicationTokenFactory: null)
    {
    }

    internal MsalTokenClient(
        A2AClientAuthenticationOptions options,
        Func<A2AClientAuthenticationOptions, MsalTokenAcquisitionRequest, CancellationToken, Task<string>>? delegatedTokenFactory,
        Func<A2AClientAuthenticationOptions, MsalTokenAcquisitionRequest, CancellationToken, Task<string>>? applicationTokenFactory)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _delegatedTokenFactory = delegatedTokenFactory;
        _applicationTokenFactory = applicationTokenFactory;
    }

    public void Configure(A2AAgentCardAuthentication authentication)
    {
        ArgumentNullException.ThrowIfNull(authentication);
        MsalTokenAcquisitionRequest acquisitionRequest = CreateAcquisitionRequest(authentication);

        switch (authentication.Mode)
        {
            case A2AAuthMode.Delegated:
                _delegatedAcquisitionRequest = acquisitionRequest;
                _publicClientApplication = null;
                break;
            case A2AAuthMode.App:
                _applicationAcquisitionRequest = acquisitionRequest;
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
        MsalTokenAcquisitionRequest acquisitionRequest = GetConfiguredAcquisitionRequest(A2AAuthMode.Delegated);

        if (_delegatedTokenFactory is not null)
        {
            return await _delegatedTokenFactory(_options, acquisitionRequest, cancellationToken).ConfigureAwait(false);
        }

        IPublicClientApplication publicClient = GetOrCreatePublicClientApplication(acquisitionRequest);
        string[] scopes = acquisitionRequest.Scopes.ToArray();
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
        MsalTokenAcquisitionRequest acquisitionRequest = GetConfiguredAcquisitionRequest(A2AAuthMode.App);

        if (_applicationTokenFactory is not null)
        {
            return await _applicationTokenFactory(_options, acquisitionRequest, cancellationToken).ConfigureAwait(false);
        }

        IConfidentialClientApplication confidentialClient = GetOrCreateConfidentialClientApplication(acquisitionRequest);

        AuthenticationResult result = await confidentialClient
            .AcquireTokenForClient(acquisitionRequest.Scopes.ToArray())
            .WithTenantId(_options.TenantId!)
            .ExecuteAsync(cancellationToken)
            .ConfigureAwait(false);

        return result.AccessToken;
    }

    private IPublicClientApplication GetOrCreatePublicClientApplication(MsalTokenAcquisitionRequest acquisitionRequest)
    {
        _publicClientApplication ??= PublicClientApplicationBuilder.Create(_options.PublicClientId!)
            .WithAuthority(acquisitionRequest.Authority, validateAuthority: true)
            .WithDefaultRedirectUri()
            .Build();

        return _publicClientApplication;
    }

    private IConfidentialClientApplication GetOrCreateConfidentialClientApplication(MsalTokenAcquisitionRequest acquisitionRequest)
    {
        _confidentialClientApplication ??= ConfidentialClientApplicationBuilder.Create(_options.ConfidentialClientId!)
            .WithAuthority(acquisitionRequest.Authority, validateAuthority: true)
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

    private MsalTokenAcquisitionRequest GetConfiguredAcquisitionRequest(A2AAuthMode mode)
    {
        MsalTokenAcquisitionRequest? acquisitionRequest = mode == A2AAuthMode.Delegated
            ? _delegatedAcquisitionRequest
            : _applicationAcquisitionRequest;
        return acquisitionRequest ?? throw new InvalidOperationException(
            $"The Agent Card has not configured a {mode} OAuth flow.");
    }

    private static MsalTokenAcquisitionRequest CreateAcquisitionRequest(A2AAgentCardAuthentication authentication)
    {
        Uri authority = GetAuthority(authentication.TokenUrl);
        Uri? deviceAuthorizationUrl = authentication.Mode == A2AAuthMode.Delegated
            ? ValidateDeviceAuthorizationEndpoint(authority, authentication.DeviceAuthorizationUrl)
            : null;

        return new MsalTokenAcquisitionRequest(authority, deviceAuthorizationUrl, authentication.Scopes);
    }

    private static Uri GetAuthority(string tokenUrl)
    {
        if (!Uri.TryCreate(tokenUrl, UriKind.Absolute, out Uri? endpoint))
        {
            throw new InvalidOperationException("The Agent Card token endpoint is malformed.");
        }

        string path = endpoint.AbsolutePath;
        const string TokenPath = "/oauth2/v2.0/token";
        if (!endpoint.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !path.EndsWith(TokenPath, StringComparison.OrdinalIgnoreCase)
            || path.Length == TokenPath.Length
            || !string.IsNullOrEmpty(endpoint.Query)
            || !string.IsNullOrEmpty(endpoint.Fragment))
        {
            throw new InvalidOperationException(
                $"The Agent Card token endpoint '{tokenUrl}' is not a supported Microsoft Entra token endpoint.");
        }

        return new UriBuilder(endpoint.Scheme, endpoint.Host, endpoint.Port, path[..^TokenPath.Length]).Uri;
    }

    private static Uri ValidateDeviceAuthorizationEndpoint(Uri authority, string? deviceAuthorizationUrl)
    {
        if (!Uri.TryCreate(deviceAuthorizationUrl, UriKind.Absolute, out Uri? deviceEndpoint)
            || !deviceEndpoint.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The Agent Card device authorization endpoint is malformed.");
        }

        string expectedPath = $"{authority.AbsolutePath.TrimEnd('/')}/oauth2/v2.0/devicecode";
        var expectedEndpoint = new UriBuilder(authority.Scheme, authority.Host, authority.Port, expectedPath).Uri;
        bool sameAuthority = Uri.Compare(
            deviceEndpoint,
            expectedEndpoint,
            UriComponents.SchemeAndServer,
            UriFormat.Unescaped,
            StringComparison.OrdinalIgnoreCase) == 0;
        if (!sameAuthority
            || !deviceEndpoint.AbsolutePath.Equals(expectedPath, StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(deviceEndpoint.Query)
            || !string.IsNullOrEmpty(deviceEndpoint.Fragment))
        {
            throw new InvalidOperationException(
                $"The Agent Card device authorization endpoint must use the same scheme, host, port, and authority tenant path as its token endpoint and the expected Entra device-code path '{expectedEndpoint}'.");
        }

        return deviceEndpoint;
    }
}

internal sealed class MsalTokenAcquisitionRequest(
    Uri authority,
    Uri? deviceAuthorizationUrl,
    IReadOnlyList<string> scopes)
{
    public Uri Authority { get; } = authority;

    public Uri? DeviceAuthorizationUrl { get; } = deviceAuthorizationUrl;

    public IReadOnlyList<string> Scopes { get; } = scopes;
}
