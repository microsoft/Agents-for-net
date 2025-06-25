// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.AspNetCore.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore
{
    internal class ActivityStreamedResponseWriter : IChannelResponseWriter
    {
        private const string ActivityEventTemplate = "event: activity\r\ndata: {0}\r\n\r\n";
        private const string InvokeResponseEventTemplate = "event: invokeResponse\r\ndata: {0}\r\n\r\n";

        public Task ResponseBegin(HttpResponse httpResponse, CancellationToken cancellationToken = default)
        {
            httpResponse.ContentType = "text/event-stream";
            return Task.CompletedTask;
        }

        public async Task WriteActivity(HttpResponse httpResponse, IActivity activity, CancellationToken cancellationToken = default)
        {
            await httpResponse.Body.WriteAsync(Encoding.UTF8.GetBytes(string.Format(ActivityEventTemplate, ProtocolJsonSerializer.ToJson(activity))), cancellationToken);
            await httpResponse.Body.FlushAsync(cancellationToken);
        }

        public async Task ResponseEnd(HttpResponse httpResponse, object data, CancellationToken cancellationToken = default)
        {
            if (data is InvokeResponse invokeResponse)
            {
                if (invokeResponse?.Body != null)
                {
                    await httpResponse.Body.WriteAsync(Encoding.UTF8.GetBytes(string.Format(InvokeResponseEventTemplate, ProtocolJsonSerializer.ToJson(invokeResponse))), cancellationToken);
                    await httpResponse.Body.FlushAsync(cancellationToken);
                }
            }
        }
    }
}
