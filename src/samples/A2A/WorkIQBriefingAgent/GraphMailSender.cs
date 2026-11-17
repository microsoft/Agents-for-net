// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace WorkIQBriefingAgent;

internal sealed class GraphMailSender(IHttpClientFactory httpClientFactory) : IMailSender
{
    public const string HttpClientName = "GraphMail";

    public async Task<MailDeliveryState> SendAsync(BriefingMail mail, string accessToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        // CreateClient returns a new HttpClient, so this user's token cannot overwrite another user's.
        using var client = httpClientFactory.CreateClient(HttpClientName);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await client.PostAsJsonAsync("me/sendMail", new
        {
            message = new
            {
                subject = mail.Subject,
                body = new { contentType = "Text", content = mail.Body },
                toRecipients = mail.Recipients.Select(address => new { emailAddress = new { address } }),
            },
            saveToSentItems = true,
        }, cancellationToken);

        // Graph accepts a send asynchronously. A 202 is not proof of delivery to the mailbox.
        if (response.StatusCode == HttpStatusCode.Accepted)
        {
            return MailDeliveryState.Accepted;
        }

        // Do not install a retry policy on this client: retrying sendMail can send another email.
        return (int)response.StatusCode is >= 400 and < 500 && response.StatusCode != HttpStatusCode.RequestTimeout
            ? MailDeliveryState.Rejected
            : MailDeliveryState.Unknown;
    }
}
