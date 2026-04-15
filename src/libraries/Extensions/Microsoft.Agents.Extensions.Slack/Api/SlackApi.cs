// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Slack.Api;

public static class SlackApi
{
    private const string SlackApiBase = "https://slack.com/api";
    private static readonly HttpClient HttpClient = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task<SlackResponse> CallAsync(string method, object? options = null, string token = "")
    {
        try
        {
            var json = JsonSerializer.Serialize(options ?? new { }, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, $"{SlackApiBase}/{method}")
            {
                Content = content
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await HttpClient.SendAsync(request);
            var text = await response.Content.ReadAsStringAsync();

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

            if (!response.IsSuccessStatusCode || !data.Ok)
            {
                throw new Exception($"Slack API error on {method} (HTTP {(int)response.StatusCode}):\n{text}");
            }

            return data;
        }
        catch (Exception ex)
        {
            return new SlackResponse { Ok = false, Error = ex.Message };
        }
    }
}