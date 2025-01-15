﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.Agents.Core.Teams.Models
{
    /// <summary>
    /// Messaging extension query.
    /// </summary>
    public class MessagingExtensionQuery
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MessagingExtensionQuery"/> class.
        /// </summary>
        public MessagingExtensionQuery()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessagingExtensionQuery"/> class.
        /// </summary>
        /// <param name="commandId">Id of the command assigned by Bot.</param>
        /// <param name="parameters">Parameters for the query.</param>
        /// <param name="queryOptions">The query options.</param>
        /// <param name="state">State parameter passed back to the bot after authentication/configuration flow.</param>
        public MessagingExtensionQuery(string commandId = default, IList<MessagingExtensionParameter> parameters = default, MessagingExtensionQueryOptions queryOptions = default, string state = default)
        {
            CommandId = commandId;
            Parameters = parameters;
            QueryOptions = queryOptions;
            State = state;
        }

        /// <summary>
        /// Gets or sets ID of the command assigned by Bot.
        /// </summary>
        /// <value>The ID of the command assigned by the Bot.</value>
        public string CommandId { get; set; }

        /// <summary>
        /// Gets or sets parameters for the query.
        /// </summary>
        /// <value>The parameters for the query.</value>
#pragma warning disable CA2227 // Collection properties should be read only (we can't change this without breaking compat).
        public IList<MessagingExtensionParameter> Parameters { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only

        /// <summary>
        /// Gets or sets the query options.
        /// </summary>
        /// <value>The query options.</value>
        public MessagingExtensionQueryOptions QueryOptions { get; set; }

        /// <summary>
        /// Gets or sets state parameter passed back to the bot after
        /// authentication/configuration flow.
        /// </summary>
        /// <value>The state parameter passed back to the bot after authentication/configuration flow.</value>
        public string State { get; set; }
    }
}
