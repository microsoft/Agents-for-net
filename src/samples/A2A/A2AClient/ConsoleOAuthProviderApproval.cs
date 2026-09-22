// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Samples.A2AClient;

internal sealed class ConsoleOAuthProviderApproval : IOAuthProviderApproval
{
    private readonly TextReader _input;
    private readonly TextWriter _output;

    public ConsoleOAuthProviderApproval()
        : this(Console.In, Console.Out)
    {
    }

    internal ConsoleOAuthProviderApproval(TextReader input, TextWriter output)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _output = output ?? throw new ArgumentNullException(nameof(output));
    }

    public async Task<Uri?> RequestServerUriAsync(
        Uri agentOrigin,
        IReadOnlyList<Uri> advertisedEndpoints,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agentOrigin);
        ArgumentNullException.ThrowIfNull(advertisedEndpoints);

        await _output.WriteLineAsync(
            $"OAuth metadata discovery failed for agent '{agentOrigin.AbsoluteUri}'.").ConfigureAwait(false);
        if (advertisedEndpoints.Count > 0)
        {
            await _output.WriteLineAsync("Advertised OAuth endpoints:").ConfigureAwait(false);
            foreach (Uri endpoint in advertisedEndpoints)
            {
                await _output.WriteLineAsync($"- {endpoint.AbsoluteUri}").ConfigureAwait(false);
            }
        }

        await _output.WriteLineAsync(
            "Enter an absolute HTTPS issuer or server URI to retry discovery, or press Enter to cancel.")
            .ConfigureAwait(false);

        while (true)
        {
            await _output.WriteAsync("> ").ConfigureAwait(false);

            string? line = await _input.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(line))
            {
                return null;
            }

            string value = line.Trim();
            if (Uri.TryCreate(value, UriKind.Absolute, out Uri? serverUri)
                && serverUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return serverUri;
            }

            await _output.WriteLineAsync("Please enter an absolute HTTPS URI or press Enter to cancel.")
                .ConfigureAwait(false);
        }
    }

    public async Task<bool> ApproveAsync(
        OAuthProviderApprovalRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        await _output.WriteLineAsync("Approve OAuth 2.1 dynamic client registration?").ConfigureAwait(false);
        await _output.WriteLineAsync($"Agent origin: {request.AgentOrigin.AbsoluteUri}").ConfigureAwait(false);
        await _output.WriteLineAsync(
            $"Security scheme: {request.SecuritySchemeName ?? "(in-task authorization)"}").ConfigureAwait(false);
        await _output.WriteLineAsync("Requested scopes:").ConfigureAwait(false);
        foreach (string scope in request.Scopes)
        {
            await _output.WriteLineAsync($"- {scope}").ConfigureAwait(false);
        }

        await WriteUriListAsync("Advertised OAuth endpoints:", request.AdvertisedEndpoints).ConfigureAwait(false);
        await _output.WriteLineAsync($"Discovered issuer: {request.Metadata.Issuer.AbsoluteUri}").ConfigureAwait(false);
        await _output.WriteLineAsync($"Metadata URL: {request.Metadata.MetadataUrl.AbsoluteUri}").ConfigureAwait(false);
        if (request.Metadata.AuthorizationEndpoint is not null)
        {
            await _output.WriteLineAsync(
                $"Authorization endpoint: {request.Metadata.AuthorizationEndpoint.AbsoluteUri}")
                .ConfigureAwait(false);
        }

        await _output.WriteLineAsync($"Token endpoint: {request.Metadata.TokenEndpoint.AbsoluteUri}").ConfigureAwait(false);
        await _output.WriteLineAsync(
            $"Registration endpoint: {request.Metadata.RegistrationEndpoint.AbsoluteUri}").ConfigureAwait(false);
        await _output.WriteLineAsync($"Redirect URI: {request.RedirectUri.AbsoluteUri}").ConfigureAwait(false);
        await _output.WriteAsync("Approve registration? [y/N]: ").ConfigureAwait(false);

        string? response = await _input.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        if (response is null)
        {
            return false;
        }

        string normalized = response.Trim();
        return normalized.Equals("y", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    private async Task WriteUriListAsync(string heading, IReadOnlyList<Uri> values)
    {
        await _output.WriteLineAsync(heading).ConfigureAwait(false);
        if (values.Count == 0)
        {
            await _output.WriteLineAsync("- (none)").ConfigureAwait(false);
            return;
        }

        foreach (Uri value in values)
        {
            await _output.WriteLineAsync($"- {value.AbsoluteUri}").ConfigureAwait(false);
        }
    }
}
