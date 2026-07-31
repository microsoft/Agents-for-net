// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable enable

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Slack;

internal interface ISlackFileUploader
{
    Task<string?> UploadAsync(
        byte[] content,
        string fileName,
        string channel,
        string token,
        CancellationToken cancellationToken);
}
