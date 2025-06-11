// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Validation;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Microsoft.Agents.Hosting.A2A.Models;
using ModelContextProtocol.Protocol;
using System.Net;
using System.Text;
using System.Collections.ObjectModel;
using System.Collections.Generic;

namespace Microsoft.Agents.Hosting.A2A
{
    public class A2AAdapter(IActivityTaskQueue activityTaskQueue) : ChannelAdapter, IA2AHttpAdapter
    {
        public async Task ProcessAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, CancellationToken cancellationToken = default)
        {
            if (httpRequest.Method != HttpMethods.Post)
            {
                httpResponse.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
            }
            else
            {
                var jsonRpcRequest = await A2AProtocolConverter.ReadRequestAsync<JsonRpcRequest>(httpRequest);

                if (jsonRpcRequest.Method.Equals("message/stream"))
                {
                    var (activity, contextId, taskId) = A2AProtocolConverter.CreateActivityFromRequest(jsonRpcRequest, isStreaming: true);
                    await ProcessStreamedAsync(activity, HttpHelper.GetIdentity(httpRequest), httpResponse, agent, new A2AStreamedResponseWriter(jsonRpcRequest.Id.ToString(), contextId, taskId), cancellationToken);
                }
            }
        }

        private async Task ProcessStreamedAsync(IActivity activity, ClaimsIdentity identity, HttpResponse httpResponse, IAgent agent, A2AStreamedResponseWriter writer, CancellationToken cancellationToken = default)
        {
            if (activity == null || !activity.Validate([ValidationContext.Channel, ValidationContext.Receiver]) || activity.DeliveryMode != DeliveryModes.Stream)
            {
                httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }

            InvokeResponse invokeResponse = null;

            await writer.ResponseBegin(httpResponse, cancellationToken).ConfigureAwait(false);

            // Queue the activity to be processed by the ActivityBackgroundService, and stop SynchronousRequestHandler when the
            // turn is done.
            activityTaskQueue.QueueBackgroundActivity(identity, activity, onComplete: (response) =>
            {
                ChannelResponseQueue.CompleteHandlerForConversation(activity.Conversation.Id);
                invokeResponse = response;
            });

            // block until turn is complete
            await ChannelResponseQueue.HandleResponsesAsync(activity.Conversation.Id, async (activity) =>
            {
                await writer.WriteActivity(httpResponse, activity, cancellationToken: cancellationToken).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);

            await writer.ResponseEnd(httpResponse, invokeResponse, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async Task ProcessAgentCardAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, string messagePrefix, CancellationToken cancellationToken = default)
        {
            //TODO: Most likely pass to AgentApplication to determine card contents
            var agentCard = new AgentCard()
            {
                Name = "EmptyAgent",
                Description = "Simple Echo Agent",
                Version = "0.2.0",
                Url = $"{httpRequest.Scheme}://{httpRequest.Host.Value}{messagePrefix}/",
                SecuritySchemes = new Dictionary<string, SecurityScheme>
                {
                    {
                        "jwt",
                        new HTTPAuthSecurityScheme() { Scheme = "bearer" }
                    }
                },
                DefaultInputModes = [],
                DefaultOutputModes = [],
                Skills = [],
                Capabilities = new AgentCapabilities()
                {
                    Streaming = true,
                }
            };

            httpResponse.ContentType = "application/json";
            var json = A2AProtocolConverter.ToJson(agentCard);
            await httpResponse.Body.WriteAsync(Encoding.UTF8.GetBytes(json), cancellationToken);
            await httpResponse.Body.FlushAsync(cancellationToken);
        }

        public override async Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext turnContext, IActivity[] activities, CancellationToken cancellationToken)
        {
            await ChannelResponseQueue.SendActivitiesAsync(turnContext.Activity.Conversation.Id, activities, cancellationToken);
            return [];
        }
    }
}
