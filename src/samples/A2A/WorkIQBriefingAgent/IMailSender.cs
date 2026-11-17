// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;

namespace WorkIQBriefingAgent;

internal sealed record BriefingMail(string[] Recipients, string Subject, string Body);

internal interface IMailSender
{
    Task<MailDeliveryState> SendAsync(BriefingMail mail, string accessToken, CancellationToken cancellationToken);
}
