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
    internal class ActivityStreamedResponseWriter : IStreamedResponseWriter
    {
        private const string ActivityEventTemplate = "event: activity\r\ndata: {0}\r\n";
        private const string InvokeResponseEventTemplate = "event: invokeResponse\r\ndata: {0}\r\n";

        public async Task WriteActivity(HttpResponse httpResponse, IActivity activity, CancellationToken cancellationToken)
        {
            httpResponse.ContentType = "text/event-stream";
            await httpResponse.Body.WriteAsync(Encoding.UTF8.GetBytes(string.Format(ActivityEventTemplate, ProtocolJsonSerializer.ToJson(activity))), cancellationToken);
            await httpResponse.Body.FlushAsync(cancellationToken);
        }

        public async Task WriteInvokeResponse(HttpResponse httpResponse, InvokeResponse invokeResponse, CancellationToken cancellationToken)
        {
            if (invokeResponse?.Body != null)
            {
                await httpResponse.Body.WriteAsync(Encoding.UTF8.GetBytes(string.Format(InvokeResponseEventTemplate, ProtocolJsonSerializer.ToJson(invokeResponse))), cancellationToken);
                await httpResponse.Body.FlushAsync(cancellationToken);
            }
        }
    }
}
