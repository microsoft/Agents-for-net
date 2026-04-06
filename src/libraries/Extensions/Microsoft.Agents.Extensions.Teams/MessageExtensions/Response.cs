// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.Agents.Extensions.Teams.MessageExtensions
{
    /// <summary>
    /// Factory methods for constructing <see cref="Microsoft.Teams.Api.MessageExtensions.Response"/> objects
    /// returned from message extension handlers.
    /// </summary>
    public static class Response
    {
        /// <summary>
        /// Creates a <see cref="Microsoft.Teams.Api.MessageExtensions.Response"/> wrapping the given result.
        /// </summary>
        /// <param name="result">The result to include in the response.</param>
        /// <param name="cacheInfo">Optional cache directives for the response.</param>
        /// <returns>A response containing the provided result.</returns>
        public static Microsoft.Teams.Api.MessageExtensions.Response WithResult(Microsoft.Teams.Api.MessageExtensions.Result result, Microsoft.Teams.Api.CacheInfo? cacheInfo = null)
        {
            return new Microsoft.Teams.Api.MessageExtensions.Response
            {
                ComposeExtension = result,
                CacheInfo = cacheInfo
            };
        }

        /// <summary>
        /// Creates a <see cref="Microsoft.Teams.Api.MessageExtensions.Response"/> that displays a plain text message to the user.
        /// </summary>
        /// <param name="message">The message text to display.</param>
        /// <returns>A response containing a message-type result with the given text.</returns>
        public static Microsoft.Teams.Api.MessageExtensions.Response WithResultMessage(string message)
        {
            return WithResult(new Microsoft.Teams.Api.MessageExtensions.Result
            {
                Type = Microsoft.Teams.Api.MessageExtensions.ResultType.Message,
                Text = message
            });
        }

        /// <summary>
        /// Creates an action response that continues a task module with the specified card attachment and dimensions in
        /// Microsoft Teams.
        /// </summary>
        /// <param name="cardAttachment">The attachment containing the card to display in the task module. Cannot be null.</param>
        /// <param name="title">An optional title for the task module, providing context to the user. May be null.</param>
        /// <param name="height">The height of the task module, specified as a size value. Determines the vertical space allocated for the
        /// module.</param>
        /// <param name="width">The width of the task module, specified as a size value. Determines the horizontal space allocated for the
        /// module.</param>
        /// <param name="fallbackUrl">An optional URL to use as a fallback if the task module cannot be displayed. May be null.</param>
        /// <param name="cacheInfo">Optional cache information to control caching behavior for the task module. May be null.</param>
        /// <returns>An ActionResponse object representing the result of the task continuation action.</returns>
        public static Microsoft.Teams.Api.MessageExtensions.ActionResponse WithTaskCard(
            Microsoft.Teams.Api.Attachment cardAttachment,
            string? title,
            Microsoft.Teams.Api.TaskModules.Size height,
            Microsoft.Teams.Api.TaskModules.Size width,
            string? fallbackUrl = null,
            Microsoft.Teams.Api.CacheInfo? cacheInfo = null)
        {
            return WithTaskCard(
                cardAttachment,
                title,
                new Microsoft.Teams.Common.Union<int, Microsoft.Teams.Api.TaskModules.Size>(height),
                new Microsoft.Teams.Common.Union<int, Microsoft.Teams.Api.TaskModules.Size>(width),
                fallbackUrl,
                cacheInfo);
        }

        /// <summary>
        /// Creates an action response that prompts the user to continue a task in a Teams task module, displaying the
        /// specified card attachment with the given dimensions and optional settings.
        /// </summary>
        /// <param name="cardAttachment">The attachment containing the card to be displayed in the task module. Cannot be null.</param>
        /// <param name="title">The title to display at the top of the task module. Can be null to omit the title.</param>
        /// <param name="height">The height of the task module in pixels. Must be a positive integer.</param>
        /// <param name="width">The width of the task module in pixels. Must be a positive integer.</param>
        /// <param name="fallbackUrl">An optional URL to use as a fallback if the card cannot be displayed in the task module.</param>
        /// <param name="cacheInfo">Optional cache information to control caching behavior for the task module.</param>
        /// <returns>An ActionResponse object that represents the action to continue the task with the specified card and
        /// settings.</returns>
        public static Microsoft.Teams.Api.MessageExtensions.ActionResponse WithTaskCard(
            Microsoft.Teams.Api.Attachment cardAttachment,
            string? title,
            int height,
            int width,
            string? fallbackUrl = null,
            Microsoft.Teams.Api.CacheInfo? cacheInfo = null)
        {
            return WithTaskCard(
                cardAttachment,
                title,
                new Microsoft.Teams.Common.Union<int, Microsoft.Teams.Api.TaskModules.Size>(height),
                new Microsoft.Teams.Common.Union<int, Microsoft.Teams.Api.TaskModules.Size>(width),
                fallbackUrl,
                cacheInfo);
        }

        private static Microsoft.Teams.Api.MessageExtensions.ActionResponse WithTaskCard(
            Microsoft.Teams.Api.Attachment cardAttachment,
            string? title,
            Microsoft.Teams.Common.Union<int, Microsoft.Teams.Api.TaskModules.Size> height,
            Microsoft.Teams.Common.Union<int, Microsoft.Teams.Api.TaskModules.Size> width,
            string? fallbackUrl = null,
            Microsoft.Teams.Api.CacheInfo? cacheInfo = null)
        {
            return new Microsoft.Teams.Api.MessageExtensions.ActionResponse
            {
                Task = new Microsoft.Teams.Api.TaskModules.ContinueTask(new Microsoft.Teams.Api.TaskModules.TaskInfo
                {
                    Title = title,
                    Height = height,
                    Width = width,
                    Card = cardAttachment,
                    FallbackUrl = fallbackUrl,
                }),
                CacheInfo = cacheInfo
            };
        }

        /// <summary>
        /// Creates a response that includes a single attachment for use in a Microsoft Teams message extension result.
        /// </summary>
        /// <remarks>Use this method to send rich content as a single attachment in a message extension
        /// result, enabling enhanced user interaction within Microsoft Teams.</remarks>
        /// <param name="attachment">The attachment to include in the response. This parameter must not be null.</param>
        /// <param name="cacheInfo">An optional cache information object that specifies caching behavior for the attachment. If not provided,
        /// default caching is applied.</param>
        /// <returns>A response object containing the specified attachment, formatted according to the provided layout and cache
        /// information.</returns>
        public static Microsoft.Teams.Api.MessageExtensions.Response WithResultAttachment(
            Microsoft.Teams.Api.MessageExtensions.Attachment attachment,
            Microsoft.Teams.Api.CacheInfo? cacheInfo = null)
        {
            return WithResultAttachments([attachment], null, cacheInfo);
        }

        /// <summary>
        /// Creates a <see cref="Microsoft.Teams.Api.MessageExtensions.Response"/> that returns a list
        /// of attachments to display in the message extension result list.
        /// </summary>
        /// <param name="attachments">The attachments to include in the result.</param>
        /// <param name="layout">The layout to use; defaults to <see cref="Microsoft.Teams.Api.Attachment.Layout.List"/>.</param>
        /// <param name="cacheInfo">Optional cache directives for the response.</param>
        /// <returns>A response containing the provided attachments.</returns>
        public static Microsoft.Teams.Api.MessageExtensions.Response WithResultAttachments(
            IEnumerable<Microsoft.Teams.Api.MessageExtensions.Attachment> attachments,
            Microsoft.Teams.Api.Attachment.Layout? layout = null,
            Microsoft.Teams.Api.CacheInfo? cacheInfo = null)
        {
            var result = new Microsoft.Teams.Api.MessageExtensions.Result
            {
                Type = Microsoft.Teams.Api.MessageExtensions.ResultType.Result,
                AttachmentLayout = layout ?? Microsoft.Teams.Api.Attachment.Layout.List,
                Attachments = [.. attachments]
            };

            return WithResult(result, cacheInfo);
        }

        /// <summary>
        /// Creates a <see cref="Microsoft.Teams.Api.MessageExtensions.Response"/> that triggers
        /// a configuration flow for the message extension.
        /// </summary>
        /// <param name="configUrl">The URL to open for configuration.</param>
        /// <param name="title">The title for the configuration action.</param>
        /// <param name="text">Optional text to include in the configuration result. May be null.</param>
        /// <param name="cacheInfo">Optional cache directives for the response.</param>
        /// <returns>A response that initiates a configuration flow.</returns>
        public static Microsoft.Teams.Api.MessageExtensions.Response WithResultConfig(
            string configUrl,
            string title = "Configure",
            string text = null,
            Microsoft.Teams.Api.CacheInfo? cacheInfo = null)
        {
            return WithResult(new Microsoft.Teams.Api.MessageExtensions.Result
            {
                Type = Microsoft.Teams.Api.MessageExtensions.ResultType.Config,
                Text = text,
                SuggestedActions = new Microsoft.Teams.Api.MessageExtensions.SuggestedActions
                {
                    Actions =
                    [
                        new Microsoft.Teams.Api.Cards.Action(Microsoft.Teams.Api.Cards.ActionType.OpenUrl)
                        {
                            Value = configUrl,
                            Title = title
                        }
                    ]
                }
            }, cacheInfo);
        }
    }
}
