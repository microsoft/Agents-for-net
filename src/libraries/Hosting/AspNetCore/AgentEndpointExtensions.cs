// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App.Proactive;
using Microsoft.Agents.Core.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace Microsoft.Agents.Hosting.AspNetCore
{
    public static class AgentEndpointExtensions
    {
        // TODO: need to support more than a single IProactiveAgent

        /// <summary>
        /// Maps endpoints for handling proactive messaging operations, such as sending activities to conversations, to
        /// the specified route group in the application's endpoint routing pipeline.
        /// </summary>
        /// <remarks>
        /// /proactive/sendactivity/{conversationId} - sends an activity to a specific conversation using the conversation ID.<br/><br/>
        /// /proactive/sendactivity - sends an activity using a conversation reference record, which includes the necessary information to identify the target conversation.<br/><br/>
        /// /proactive/createconversation - creates a new conversation and sends an initial activity to it. The request payload should include the necessary information to create the conversation and the activity to be sent.<br/><br/>
        /// </remarks>
        /// <remarks>The mapped endpoints include operations for sending activities to a specific
        /// conversation, sending activities using a conversation reference, and creating new conversations. If
        /// requireAuth is set to true, all endpoints require authorization; otherwise, they allow anonymous access. The
        /// endpoints expect JSON payloads and are grouped under the specified route pattern.</remarks>
        /// <param name="endpoints">The endpoint route builder to which the proactive messaging endpoints are added.</param>
        /// <param name="requireAuth">true to require authentication for the mapped endpoints; otherwise, false. The default is true.</param>
        /// <param name="pattern">The route pattern under which the proactive messaging endpoints are grouped. The default is "/proactive".</param>
        /// <returns>An endpoint convention builder that can be used to further customize the mapped proactive messaging
        /// endpoints.</returns>
        public static IEndpointConventionBuilder MapProactive(this IEndpointRouteBuilder endpoints, bool requireAuth = true, [StringSyntax("Route")] string pattern = "/proactive")
        {
            var routeGroup = endpoints.MapGroup(pattern);
            if (requireAuth)
            {
                routeGroup.RequireAuthorization();
            }
            else
            {
                routeGroup.AllowAnonymous();
            }

            routeGroup.MapPost(
                "/sendactivity/{conversationId}",
                async (HttpRequest request, HttpResponse response, IChannelAdapter adapter, IProactiveAgent agent, string conversationId, CancellationToken cancellationToken) =>
                {
                    var activity = await HttpHelper.ReadRequestAsync<IActivity>(request).ConfigureAwait(false);

                    try
                    {
                        await agent.SendActivityAsync(adapter, conversationId, activity, cancellationToken).ConfigureAwait(false);
                    }
                    catch (KeyNotFoundException)
                    {
                        response.StatusCode = StatusCodes.Status404NotFound;
                        await response.WriteAsJsonAsync(new { error = $"Conversation with id '{conversationId}' not found." }, cancellationToken).ConfigureAwait(false);
                        return;
                    }
                })
                .WithMetadata(new AcceptsMetadata(["application/json"]));

            routeGroup.MapPost(
                "/sendactivity",
                async (HttpRequest request, HttpResponse response, IChannelAdapter adapter, IProactiveAgent agent, CancellationToken cancellationToken) =>
                {
                    var recordRequest = await HttpHelper.ReadRequestAsync<SendToConversationRecord>(request).ConfigureAwait(false);

                    if (recordRequest.ConversationReferenceRecord == null)
                    {
                        response.StatusCode = StatusCodes.Status400BadRequest;
                        await response.WriteAsJsonAsync(new { error = "ConversationReferenceRecord is required." }, cancellationToken).ConfigureAwait(false);
                        return;
                    }

                    if (recordRequest.Activity == null)
                    {
                        response.StatusCode = StatusCodes.Status400BadRequest;
                        await response.WriteAsJsonAsync(new { error = "Activity is required." }, cancellationToken).ConfigureAwait(false);
                        return;
                    }

                     await agent.SendActivityAsync(adapter, recordRequest.ConversationReferenceRecord, recordRequest.Activity, cancellationToken).ConfigureAwait(false);
                })
                .WithMetadata(new AcceptsMetadata(["application/json"]));

            routeGroup.MapPost(
                "/createconversation",
                async (HttpRequest request, HttpResponse response, IChannelAdapter adapter, IProactiveAgent agent, CancellationToken cancellationToken) =>
                {
                    // TODO: call IProactiveAgent
                    // request -> IProactiveAgent -> AgentApplication.Proactive.CreateConversation
                })
                .WithMetadata(new AcceptsMetadata(["application/json"]));

            return routeGroup;
        }

        class SendToConversationRecord
        {
            public ConversationReferenceRecord ConversationReferenceRecord { get; set; }
            public IActivity Activity { get; set; } = default!;
        }
    }
}