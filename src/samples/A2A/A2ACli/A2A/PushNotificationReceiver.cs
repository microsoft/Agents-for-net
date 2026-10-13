// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using A2A;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.Agents.Samples.A2ACli.A2A;

internal sealed class PushNotificationReceiver : IAsyncDisposable
{
    private readonly WebApplication _application;

    private PushNotificationReceiver(WebApplication application, Uri notificationUri)
    {
        _application = application;
        NotificationUri = notificationUri;
    }

    internal Uri NotificationUri { get; }

    internal static async Task<PushNotificationReceiver> StartAsync(
        Uri receiverUri,
        Action<StreamResponse> notificationHandler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(receiverUri);
        ArgumentNullException.ThrowIfNull(notificationHandler);

        if (!receiverUri.IsAbsoluteUri
            || (receiverUri.Scheme != Uri.UriSchemeHttp && receiverUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                "The push notification receiver URL must be an absolute HTTP or HTTPS URL.",
                nameof(receiverUri));
        }

        if (!receiverUri.IsLoopback)
        {
            throw new ArgumentException(
                "The unauthenticated push notification receiver must use a loopback URL.",
                nameof(receiverUri));
        }

        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls(receiverUri.GetLeftPart(UriPartial.Authority));

        WebApplication application = builder.Build();
        string callbackPath = $"{receiverUri.AbsolutePath.TrimEnd('/')}/notify";
        application.MapPost(callbackPath, async context =>
        {
            StreamResponse? notification;
            try
            {
                notification = await JsonSerializer.DeserializeAsync<StreamResponse>(
                    context.Request.Body,
                    A2AJsonUtilities.DefaultOptions,
                    context.RequestAborted).ConfigureAwait(false);
            }
            catch (JsonException)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            if (notification is null)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            notificationHandler(notification);
            context.Response.StatusCode = StatusCodes.Status200OK;
        });

        try
        {
            await application.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await application.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        string boundAddress = application.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()?
            .Addresses
            .Single()
            ?? throw new InvalidOperationException(
                "The push notification receiver did not report a listening address.");
        var notificationUri = new UriBuilder(boundAddress)
        {
            Path = callbackPath,
        }.Uri;

        return new PushNotificationReceiver(application, notificationUri);
    }

    public async ValueTask DisposeAsync()
    {
        await _application.StopAsync().ConfigureAwait(false);
        await _application.DisposeAsync().ConfigureAwait(false);
    }
}
