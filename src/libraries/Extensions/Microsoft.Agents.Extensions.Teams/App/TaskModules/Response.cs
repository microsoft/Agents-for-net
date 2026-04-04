// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using Microsoft.Teams.Common;

namespace Microsoft.Agents.Extensions.Teams.App.TaskModules;

/// <summary>
/// Factory methods for constructing <see cref="Microsoft.Teams.Api.TaskModules.Response"/> objects
/// returned from task module handlers.
/// </summary>
public static class Response
{
    /// <summary>
    /// Creates a <see cref="Microsoft.Teams.Api.TaskModules.Response"/> that continues the task
    /// module with an Adaptive Card displayed in a dialog.
    /// </summary>
    /// <param name="card">The Adaptive Card to display.</param>
    /// <param name="title">Title of the dialog.</param>
    /// <param name="height">Height of the dialog as a pixel value.</param>
    /// <param name="width">Width of the dialog as a pixel value.</param>
    /// <param name="fallbackUrl">Fallback URL if the client cannot render the dialog.</param>
    /// <param name="cacheInfo">Optional cache directives for the response.</param>
    /// <returns>A response that opens a dialog showing the provided card.</returns>
    public static Microsoft.Teams.Api.TaskModules.Response WithCard(
        Microsoft.Teams.Api.Attachment card,
        string? title = null,
        int? height = null,
        int? width = null,
        string? fallbackUrl = null,
        Microsoft.Teams.Api.CacheInfo? cacheInfo = null)
    {
        AssertionHelpers.ThrowIfNull(card, nameof(card));
        var taskInfo = new Microsoft.Teams.Api.TaskModules.TaskInfo
        {
            Card = card,
            Title = title,
            FallbackUrl = fallbackUrl
        };
        if (height.HasValue)
            taskInfo.Height = new Union<int, Microsoft.Teams.Api.TaskModules.Size>(height.Value);
        if (width.HasValue)
            taskInfo.Width = new Union<int, Microsoft.Teams.Api.TaskModules.Size>(width.Value);

        return new Microsoft.Teams.Api.TaskModules.Response(
            new Microsoft.Teams.Api.TaskModules.ContinueTask(taskInfo))
        {
            CacheInfo = cacheInfo
        };
    }

    /// <summary>
    /// Creates a <see cref="Microsoft.Teams.Api.TaskModules.Response"/> that continues the task
    /// module with an Adaptive Card displayed in a dialog using a predefined size.
    /// </summary>
    /// <param name="card">The card to display.</param>
    /// <param name="title">Title of the dialog.</param>
    /// <param name="height">Height of the dialog using a predefined size.</param>
    /// <param name="width">Width of the dialog using a predefined size.</param>
    /// <param name="fallbackUrl">Fallback URL if the client cannot render the dialog.</param>
    /// <param name="cacheInfo">Optional cache directives for the response.</param>
    /// <returns>A response that opens a dialog showing the provided card.</returns>
    public static Microsoft.Teams.Api.TaskModules.Response WithCard(
        Microsoft.Teams.Api.Attachment card,
        string? title,
        Microsoft.Teams.Api.TaskModules.Size height,
        Microsoft.Teams.Api.TaskModules.Size width,
        string? fallbackUrl = null,
        Microsoft.Teams.Api.CacheInfo? cacheInfo = null)
    {
        AssertionHelpers.ThrowIfNull(card, nameof(card));
        var taskInfo = new Microsoft.Teams.Api.TaskModules.TaskInfo
        {
            Card = card,
            Title = title,
            Height = new Union<int, Microsoft.Teams.Api.TaskModules.Size>(height),
            Width = new Union<int, Microsoft.Teams.Api.TaskModules.Size>(width),
            FallbackUrl = fallbackUrl
        };

        return new Microsoft.Teams.Api.TaskModules.Response(
            new Microsoft.Teams.Api.TaskModules.ContinueTask(taskInfo))
        {
            CacheInfo = cacheInfo
        };
    }

    /// <summary>
    /// Creates a <see cref="Microsoft.Teams.Api.TaskModules.Response"/> that continues the task
    /// module by loading a URL in a dialog.
    /// </summary>
    /// <param name="url">The URL to load in the dialog.</param>
    /// <param name="title">Title of the dialog.</param>
    /// <param name="height">Height of the dialog as a pixel value.</param>
    /// <param name="width">Width of the dialog as a pixel value.</param>
    /// <param name="fallbackUrl">Fallback URL if the client cannot render the dialog.</param>
    /// <param name="completionBotId">App ID of the bot to send the result to when the dialog closes.</param>
    /// <param name="cacheInfo">Optional cache directives for the response.</param>
    /// <returns>A response that opens a dialog loading the provided URL.</returns>
    public static Microsoft.Teams.Api.TaskModules.Response WithUrl(
        string url,
        string? title = null,
        int? height = null,
        int? width = null,
        string? fallbackUrl = null,
        string? completionBotId = null,
        Microsoft.Teams.Api.CacheInfo? cacheInfo = null)
    {
        AssertionHelpers.ThrowIfNullOrWhiteSpace(url, nameof(url));
        var taskInfo = new Microsoft.Teams.Api.TaskModules.TaskInfo
        {
            Url = url,
            Title = title,
            FallbackUrl = fallbackUrl,
            CompletionBotId = completionBotId
        };
        if (height.HasValue)
            taskInfo.Height = new Union<int, Microsoft.Teams.Api.TaskModules.Size>(height.Value);
        if (width.HasValue)
            taskInfo.Width = new Union<int, Microsoft.Teams.Api.TaskModules.Size>(width.Value);

        return new Microsoft.Teams.Api.TaskModules.Response(
            new Microsoft.Teams.Api.TaskModules.ContinueTask(taskInfo))
        {
            CacheInfo = cacheInfo
        };
    }

    /// <summary>
    /// Creates a <see cref="Microsoft.Teams.Api.TaskModules.Response"/> that continues the task
    /// module by loading a URL in a dialog using a predefined size.
    /// </summary>
    /// <param name="url">The URL to load in the dialog.</param>
    /// <param name="title">Title of the dialog.</param>
    /// <param name="height">Height of the dialog using a predefined size.</param>
    /// <param name="width">Width of the dialog using a predefined size.</param>
    /// <param name="fallbackUrl">Fallback URL if the client cannot render the dialog.</param>
    /// <param name="completionBotId">App ID of the bot to send the result to when the dialog closes.</param>
    /// <param name="cacheInfo">Optional cache directives for the response.</param>
    /// <returns>A response that opens a dialog loading the provided URL.</returns>
    public static Microsoft.Teams.Api.TaskModules.Response WithUrl(
        string url,
        string? title,
        Microsoft.Teams.Api.TaskModules.Size height,
        Microsoft.Teams.Api.TaskModules.Size width,
        string? fallbackUrl = null,
        string? completionBotId = null,
        Microsoft.Teams.Api.CacheInfo? cacheInfo = null)
    {
        AssertionHelpers.ThrowIfNullOrWhiteSpace(url, nameof(url));
        var taskInfo = new Microsoft.Teams.Api.TaskModules.TaskInfo
        {
            Url = url,
            Title = title,
            Height = new Union<int, Microsoft.Teams.Api.TaskModules.Size>(height),
            Width = new Union<int, Microsoft.Teams.Api.TaskModules.Size>(width),
            FallbackUrl = fallbackUrl,
            CompletionBotId = completionBotId
        };

        return new Microsoft.Teams.Api.TaskModules.Response(
            new Microsoft.Teams.Api.TaskModules.ContinueTask(taskInfo))
        {
            CacheInfo = cacheInfo
        };
    }

    /// <summary>
    /// Creates a <see cref="Microsoft.Teams.Api.TaskModules.Response"/> that dismisses the task
    /// module and displays a message to the user.
    /// </summary>
    /// <param name="message">The message to display after the task module closes.</param>
    /// <param name="cacheInfo">Optional cache directives for the response.</param>
    /// <returns>A response that closes the dialog and shows the given message.</returns>
    public static Microsoft.Teams.Api.TaskModules.Response WithMessage(
        string message,
        Microsoft.Teams.Api.CacheInfo? cacheInfo = null)
    {
        AssertionHelpers.ThrowIfNullOrWhiteSpace(message, nameof(message));
        return new Microsoft.Teams.Api.TaskModules.Response(
            new Microsoft.Teams.Api.TaskModules.MessageTask(message))
        {
            CacheInfo = cacheInfo
        };
    }
}
