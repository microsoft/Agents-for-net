// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.AspNetCore.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore
{
    public interface IChannelResponseHandler
    {
        Task ResponseBegin(HttpResponse httpResponse, CancellationToken cancellationToken = default);

        Task OnResponse(HttpResponse httpResponse, IActivity activity, CancellationToken cancellationToken = default);

        Task ResponseEnd(HttpResponse httpResponse, object data, CancellationToken cancellationToken = default);
    }
}
