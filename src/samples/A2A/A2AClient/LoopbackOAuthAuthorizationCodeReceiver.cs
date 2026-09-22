// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Samples.A2AClient;

internal sealed class LoopbackOAuthAuthorizationCodeReceiver : IOAuthAuthorizationCodeReceiver
{
    private readonly Action<Uri> _openBrowser;

    public LoopbackOAuthAuthorizationCodeReceiver()
        : this(OpenBrowser)
    {
    }

    internal LoopbackOAuthAuthorizationCodeReceiver(Action<Uri> openBrowser)
    {
        _openBrowser = openBrowser ?? throw new ArgumentNullException(nameof(openBrowser));
    }

    public async Task<string> ReceiveCodeAsync(
        Uri authorizationUri,
        Uri redirectUri,
        string expectedState,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorizationUri);
        ArgumentNullException.ThrowIfNull(redirectUri);
        OAuthEndpointValidator.EnsureSupportedLoopbackRedirectUri(redirectUri);

        using var listener = new HttpListener();
        listener.Prefixes.Add(redirectUri.AbsoluteUri);
        listener.Start();
        _openBrowser(authorizationUri);

        HttpListenerContext context = await listener.GetContextAsync()
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            string? error = context.Request.QueryString["error"];
            string? errorDescription = context.Request.QueryString["error_description"];
            if (!string.IsNullOrWhiteSpace(error))
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(errorDescription)
                        ? $"OAuth authorization returned error '{error}'."
                        : $"OAuth authorization returned error '{error}'. {errorDescription}");
            }

            string? state = context.Request.QueryString["state"];
            if (!string.Equals(state, expectedState, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("OAuth authorization response state validation failed.");
            }

            string? code = context.Request.QueryString["code"];
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new InvalidOperationException("OAuth authorization response is missing the authorization code.");
            }

            await WriteResponseAsync(
                context.Response,
                "Authorization complete. You can close this window.",
                cancellationToken).ConfigureAwait(false);
            return code;
        }
        catch
        {
            await WriteResponseAsync(
                context.Response,
                "Authorization failed. Return to the A2A client for details.",
                cancellationToken).ConfigureAwait(false);
            throw;
        }
        finally
        {
            context.Response.Close();
        }
    }

    private static void OpenBrowser(Uri authorizationUri)
    {
        Process.Start(new ProcessStartInfo(authorizationUri.AbsoluteUri)
        {
            UseShellExecute = true,
        });
    }

    private static async Task WriteResponseAsync(
        HttpListenerResponse response,
        string message,
        CancellationToken cancellationToken)
    {
        byte[] body = Encoding.UTF8.GetBytes(message);
        response.ContentType = "text/plain; charset=utf-8";
        response.ContentLength64 = body.Length;
        await response.OutputStream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
    }
}
