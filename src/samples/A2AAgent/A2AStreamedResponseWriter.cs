// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.AspNetCore.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace A2AAgent
{
    internal class A2AStreamedResponseWriter(string requestId, string taskId) : IStreamedResponseWriter
    {
        private const string ArtifactUpdateTemplate = "event: message\r\ndata: {0}\r\n";

        public async Task WriteActivity(HttpResponse httpResponse, IActivity activity, CancellationToken cancellationToken)
        {
            httpResponse.ContentType ??= "text/event-stream";

            var response = A2AProtocolConverter.CreateStreamResponseFromActivity(requestId, taskId, activity);
            await httpResponse.Body.WriteAsync(Encoding.UTF8.GetBytes(string.Format(ArtifactUpdateTemplate, response)), cancellationToken).ConfigureAwait(false);
            await httpResponse.Body.FlushAsync(cancellationToken);
        }

        public Task WriteInvokeResponse(HttpResponse httpResponse, InvokeResponse invokeResponse, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
