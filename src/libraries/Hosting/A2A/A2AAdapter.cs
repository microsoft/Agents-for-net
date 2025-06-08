// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Microsoft.Agents.Hosting.A2A.Models;
using ModelContextProtocol.Protocol;
using System.Net;
using System.Text;

namespace Microsoft.Agents.Hosting.A2A
{
    public class A2AAdapter : ChannelAdapter, IA2AHttpAdapter
    {
        private readonly IActivityTaskQueue _activityTaskQueue;

        public A2AAdapter(IActivityTaskQueue activityTaskQueue) 
        {
            _activityTaskQueue = activityTaskQueue;
        }

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
                    await ProcessAsync(activity, HttpHelper.GetIdentity(httpRequest), httpResponse, agent, new A2AStreamedResponseWriter(jsonRpcRequest.Id.ToString(), contextId, taskId), cancellationToken);
                }
            }
        }

        public async Task ProcessAsync(IActivity activity, ClaimsIdentity identity, HttpResponse httpResponse, IAgent agent, IStreamedResponseWriter writer = null, CancellationToken cancellationToken = default)
        {
            if (!IsValidChannelActivity(activity) || activity.DeliveryMode != DeliveryModes.Stream)
            {
                httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }

            InvokeResponse invokeResponse = null;

            // Queue the activity to be processed by the ActivityBackgroundService, and stop SynchronousRequestHandler when the
            // turn is done.
            _activityTaskQueue.QueueBackgroundActivity(identity, activity, onComplete: (response) =>
            {
                StreamedResponseHandler.CompleteHandlerForConversation(activity.Conversation.Id);
                invokeResponse = response;
            });

            writer ??= StreamedResponseHandler.DefaultWriter;
            await writer.StreamBegin(httpResponse).ConfigureAwait(false);

            // block until turn is complete
            await StreamedResponseHandler.HandleResponsesAsync(activity.Conversation.Id, async (activity) =>
            {
                await writer.WriteActivity(httpResponse, activity, cancellationToken: cancellationToken).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);

            await writer.StreamEnd(httpResponse, invokeResponse, cancellationToken: cancellationToken).ConfigureAwait(false);
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
                DefaultInputModes = [],
                DefaultOutputModes = [],
                Skills = [],
                Capabilities = new AgentCapabilities()
                {
                    Streaming = true,
                }
            };

            httpResponse.ContentType = "application/json";
            await httpResponse.Body.WriteAsync(Encoding.UTF8.GetBytes(A2AProtocolConverter.ToJson(agentCard)), cancellationToken);
            await httpResponse.Body.FlushAsync(cancellationToken);
        }

        public override async Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext turnContext, IActivity[] activities, CancellationToken cancellationToken)
        {
            await StreamedResponseHandler.SendActivitiesAsync(turnContext.Activity.Conversation.Id, activities, cancellationToken);
            return [];
        }

        //TODO: copied from CloudAdapter.  Consolidate.
        private static bool IsValidChannelActivity(IActivity activity)
        {
            if (activity == null)
            {
                System.Diagnostics.Trace.WriteLine("BadRequest: Missing activity");
                return false;
            }

            if (string.IsNullOrEmpty(activity.Type?.ToString()))
            {
                System.Diagnostics.Trace.WriteLine("BadRequest: Missing activity type");
                return false;
            }

            if (string.IsNullOrEmpty(activity.Conversation?.Id))
            {
                System.Diagnostics.Trace.WriteLine("BadRequest: Missing Conversation.Id");
                return false;
            }

            return true;
        }
    }
}
