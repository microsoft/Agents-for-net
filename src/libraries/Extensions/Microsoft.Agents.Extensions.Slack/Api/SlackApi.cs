// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Slack.Api;

/// <summary>
/// Provides methods for calling the Slack Web API using HTTP requests. This class enables integration with Slack by
/// sending API method calls and handling responses.    
/// </summary>
/// <remarks>Instances of this class use an injected IHttpClientFactory to create named HTTP clients for making requests
/// to Slack. The class is intended for use in applications that need to interact with Slack's API endpoints, such as
/// sending messages, retrieving user information, or managing channels.</remarks>
public class SlackApi
{
    private const string SlackApiBase = "https://slack.com/api";
    private IHttpClientFactory _httpClientFactory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public SlackApi(IHttpClientFactory httpClientFactory)
    {
        AssertionHelpers.ThrowIfNull(httpClientFactory, nameof(httpClientFactory));
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Invokes a Slack Web API method asynchronously using the specified method name, options, and authentication
    /// token.
    /// </summary>
    /// <remarks>If the API call fails or an exception occurs, the returned SlackResponse will indicate
    /// failure and include the error message. This method does not throw exceptions for API errors; instead, errors are
    /// reported in the SlackResponse.  This method uses a HTTP client named "SlackApi" created by the injected IHttpClientFactory.</remarks>
    /// <param name="method">The name of the Slack API method to call. Cannot be null, empty, or whitespace.</param>
    /// <param name="options">An object containing parameters to include in the API request body. May be null if no parameters are required.</param>
    /// <param name="token">The OAuth access token used to authenticate the request. If empty, the request may fail unless the API method
    /// allows unauthenticated access.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a SlackResponse object with the API
    /// response data. If the call fails, the SlackResponse will have Ok set to false and an error message.</returns>
    public async Task<SlackResponse> CallAsync(string method, object? options = null, string token = "", CancellationToken cancellationToken = default)
    {
        AssertionHelpers.ThrowIfNullOrWhiteSpace(method, nameof(method));
        AssertionHelpers.ThrowIfNull(options, nameof(options));

        var json = options is string str ? str : JsonSerializer.Serialize(options ?? new { }, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, $"{SlackApiBase}/{method}")
        {
            Content = content
        };

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
            
        using var httpClient = _httpClientFactory.CreateClient(nameof(SlackApi));
        var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        SlackResponse data;
        try
        {
            data = JsonSerializer.Deserialize<SlackResponse>(text)
                ?? throw new Exception("Null response from Slack");
        }
        catch
        {
            throw new Exception($"Slack API error on {method} (HTTP {(int)response.StatusCode}):\n{text}");
        }

        if (!response.IsSuccessStatusCode || !data.ok)
        {
            throw new SlackResponseException($"Slack API error on {method} (HTTP {(int)response.StatusCode}):\n{text}");
        }

        return data;
    }
}