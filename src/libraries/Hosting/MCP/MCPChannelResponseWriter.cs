// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.AspNetCore.Http;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.MCP
{
    public class MCPChannelResponseWriter() : IChannelResponseWriter
    {
        public const string MessageTemplate = "event: message\r\ndata: {0}\r\n\r\n";

        public Task ResponseBegin(HttpResponse httpResponse, CancellationToken cancellationToken = default)
        {
            httpResponse.StatusCode = (int) HttpStatusCode.OK;
            httpResponse.ContentType = "text/event-stream";
            return Task.CompletedTask;
        }

        public async Task WriteActivity(HttpResponse httpResponse, IActivity activity, CancellationToken cancellationToken = default)
        {
            var response = MCPProtocolConverter.CreateStreamMessageFromActivity(activity);
            var sse = string.Format(MessageTemplate, response);
            await httpResponse.Body.WriteAsync(Encoding.UTF8.GetBytes(sse), cancellationToken).ConfigureAwait(false);
            await httpResponse.Body.FlushAsync(cancellationToken);
        }

        public Task ResponseEnd(HttpResponse httpResponse, object data, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
