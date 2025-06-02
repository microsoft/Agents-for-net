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
    public class A2AStreamedResponseWriter(string requestId, string contextId, string taskId) : IStreamedResponseWriter
    {
        private const string MessageTemplate = "event: message\r\ndata: {0}\r\n\r\n";

        public Task StreamBegin(HttpResponse httpResponse)
        {
            httpResponse.ContentType = "text/event-stream";
            return Task.CompletedTask;
        }

        public async Task WriteActivity(HttpResponse httpResponse, IActivity activity, CancellationToken cancellationToken)
        {
            var response = A2AProtocolConverter.CreateStreamMessageFromActivity(requestId, contextId, taskId, activity);
            var sse = string.Format(MessageTemplate, response);
            await httpResponse.Body.WriteAsync(Encoding.UTF8.GetBytes(sse), cancellationToken).ConfigureAwait(false);
            await httpResponse.Body.FlushAsync(cancellationToken);
        }

        public Task StreamEnd(HttpResponse httpResponse, object data, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
