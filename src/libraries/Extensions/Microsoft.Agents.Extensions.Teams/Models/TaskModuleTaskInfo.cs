﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Metadata for a Task Module.
    /// </summary>
    public class TaskModuleTaskInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TaskModuleTaskInfo"/> class.
        /// </summary>
        public TaskModuleTaskInfo()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TaskModuleTaskInfo"/> class.
        /// </summary>
        /// <param name="title">Appears below the app name and to the right of the app icon.</param>
        /// <param name="height">This can be a number, representing the task module's height in pixels, or a string, one of: small, medium, large.</param>
        /// <param name="width">This can be a number, representing the task module's width in pixels, or a string, one of: small, medium, large.</param>
        /// <param name="url">The URL of what is loaded as an iframe inside the task module. One of url or card is required.</param>
        /// <param name="card">The JSON for the Adaptive card to appear in the task module.</param>
        /// <param name="fallbackUrl">If a client does not support the task module feature, this URL is opened in a browser tab.</param>
        /// <param name="completionBotId">Specifies a bot App ID to send the result of the user's interaction with the task module to.
        /// If specified, the bot will receive a task/submit invoke event with a JSON object in the event payload.</param>
        public TaskModuleTaskInfo(string title = default, object height = default, object width = default, string url = default, Attachment card = default, string fallbackUrl = default, string completionBotId = default(string))
        {
            Title = title;
            Height = height;
            Width = width;
            Url = url;
            Card = card;
            FallbackUrl = fallbackUrl;
            CompletionBotId = completionBotId;
        }

        /// <summary>
        /// Gets or sets the title that appears below the app name and to the right of the app
        /// icon.
        /// </summary>
        /// <value>The title.</value>
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets this can be a number, representing the task module's
        /// height in pixels, or a string, one of: small, medium, large.
        /// </summary>
        /// <value>The task module's height.</value>
        public object Height { get; set; }

        /// <summary>
        /// Gets or sets this can be a number, representing the task module's
        /// width in pixels, or a string, one of: small, medium, large.
        /// </summary>
        /// <value>The task module's width.</value>
        public object Width { get; set; }

        /// <summary>
        /// Gets or sets the URL of what is loaded as an iframe inside the task
        /// module. One of url or card is required.
        /// </summary>
        /// <value>The URL of what is loaded as an iframe inside the task module.</value>
        public string Url { get; set; }

        /// <summary>
        /// Gets or sets the JSON for the Adaptive card to appear in the task
        /// module.
        /// </summary>
        /// <value>The JSON for the Adaptive card to appear in the task module.</value>
        public Attachment Card { get; set; }

        /// <summary>
        /// Gets or sets if a client does not support the task module feature,
        /// this URL is opened in a browser tab.
        /// </summary>
        /// <value>The fallback URL to open in a browser tab if the client does not support the task module feature.</value>
        public string FallbackUrl { get; set; }

        /// <summary>
        /// Gets or sets Specifies a bot App ID to send the result of the user's
        /// interaction with the task module to. If specified, the bot will receive
        /// a task/submit invoke event with a JSON object in the event payload.
        /// </summary>
        /// <value>The completion bot ID.</value>
        public string CompletionBotId { get; set; }
    }
}
