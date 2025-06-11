// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Hosting.A2A.Models;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.AspNetCore.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.A2A
{
    public class A2AStreamedResponseWriter(string requestId, string contextId, string taskId) : IChannelResponseWriter
    {
        private const string MessageTemplate = "event: message\r\ndata: {0}\r\n\r\n";
        private const string UpdateTemplate = "event: {0}\r\ndata: {1}\r\n\r\n";
        private const string TaskTemplate = "event: task\r\ndata: {0}\r\n\r\n";

        private bool _inStreamingResponse = false;

        public async Task ResponseBegin(HttpResponse httpResponse, CancellationToken cancellationToken = default)
        {
            httpResponse.ContentType = "text/event-stream";
            var task = A2AProtocolConverter.CreateStreamTaskFromActivity(requestId, contextId, taskId, TaskState.Submitted);
            var status = string.Format(TaskTemplate, task);
            await WriteEvent(httpResponse, status, cancellationToken).ConfigureAwait(false);
        }

        public async Task WriteActivity(HttpResponse httpResponse, IActivity activity, CancellationToken cancellationToken = default)
        {
            var entity = activity.GetStreamingEntity();
            if (entity != null)
            {
                var isLastChunk = entity.StreamType == StreamTypes.Final;
                var isInformative = entity.StreamType == StreamTypes.Informative;

                if (!_inStreamingResponse || isInformative)
                {
                    _inStreamingResponse = true;

                    var working = A2AProtocolConverter.CreateStreamStatusUpdateFromActivity(requestId, contextId, taskId, TaskState.Working, artifactId:entity.StreamId, activity:isInformative ? activity : null);
                    var status = string.Format(UpdateTemplate, TaskStatusUpdateEvent.TaskStatusUpdateEventKind, working);

                    await WriteEvent(httpResponse, status, cancellationToken).ConfigureAwait(false);
                }

                var update = A2AProtocolConverter.CreateStreamArtifactUpdateFromActivity(requestId, contextId, taskId, activity, artifactId:entity.StreamId, append:false, lastChunk:isLastChunk);
                var artifact = string.Format(UpdateTemplate, TaskStatusUpdateEvent.TaskStatusUpdateEventKind, update);

                await WriteEvent(httpResponse, artifact, cancellationToken).ConfigureAwait(false);

                if (isLastChunk)
                {
                    _inStreamingResponse = false;
                }
            }
            else if (!activity.IsType(ActivityTypes.Typing))
            {
                var response = A2AProtocolConverter.CreateStreamMessageFromActivity(requestId, contextId, taskId, activity);
                var sse = string.Format(MessageTemplate, response);

                await WriteEvent(httpResponse, sse, cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task ResponseEnd(HttpResponse httpResponse, object data, CancellationToken cancellationToken = default)
        {
            var final = A2AProtocolConverter.CreateStreamStatusUpdateFromActivity(requestId, contextId, taskId, TaskState.Completed, isFinal:true);
            var status = string.Format(UpdateTemplate, TaskStatusUpdateEvent.TaskStatusUpdateEventKind, final);
            await WriteEvent(httpResponse, status, cancellationToken).ConfigureAwait(false);
        }

        private static async Task WriteEvent(HttpResponse httpResponse, string sse, CancellationToken cancellationToken)
        {
            System.Diagnostics.Trace.WriteLine(sse);
            await httpResponse.Body.WriteAsync(Encoding.UTF8.GetBytes(sse), cancellationToken).ConfigureAwait(false);
            await httpResponse.Body.FlushAsync(cancellationToken);
        }
    }
}
