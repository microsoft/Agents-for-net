// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.AspNetCore.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore
{
    public interface IStreamedResponseWriter
    {
        Task StreamBegin(HttpResponse httpResponse);

        Task WriteActivity(HttpResponse httpResponse, IActivity activity, CancellationToken cancellationToken);

        Task StreamEnd(HttpResponse httpResponse, object data, CancellationToken cancellationToken);
    }
}
