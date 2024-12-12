// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Protocols.Connector;
using Microsoft.Agents.Protocols.Primitives;
using Microsoft.Agents.Protocols.Serializer;
using Microsoft.Agents.Teams.Primitives;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Teams.Connector
{
    /// <summary>
    /// TeamsOperations operations.
    /// </summary>
    internal class RestTeamsOperations : ITeamsOperations
    {
        private static volatile RetryParams currentRetryPolicy;
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="RestTeamsOperations"/> class.
        /// </summary>
        /// <param name='client'>
        /// <param name="httpClient"/>
        /// Reference to the service client.
        /// </param>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when a required parameter is null.
        /// </exception>
        public RestTeamsOperations(RestTeamsConnectorClient client, HttpClient httpClient)
        {
            ArgumentNullException.ThrowIfNull(client);

            Client = client;
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        /// <summary>
        /// Gets a reference to the TeamsConnectorClient.
        /// </summary>
        /// <value>The TeamsConnectorClient.</value>
        public RestTeamsConnectorClient Client { get; private set; }

        /// <inheritdoc/>
        public async Task<ConversationList> FetchChannelListAsync(string teamId, Dictionary<string, List<string>> customHeaders = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            ArgumentNullException.ThrowIfNull(teamId);

            // Construct URL
            var url = "v3/teams/{teamId}/conversations";
            url = url.Replace("{teamId}", Uri.EscapeDataString(teamId));

            return await GetResponseAsync<ConversationList>("FetchChannelList", url, HttpMethod.Get, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<TeamDetails> FetchTeamDetailsAsync(string teamId, Dictionary<string, List<string>> customHeaders = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            ArgumentNullException.ThrowIfNull(teamId);

            // Construct URL
            var url = "v3/teams/{teamId}";
            url = url.Replace("{teamId}", Uri.EscapeDataString(teamId));

            return await GetResponseAsync<TeamDetails>("FetchTeamDetails", url, HttpMethod.Get, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<MeetingInfo> FetchMeetingInfoAsync(string meetingId, Dictionary<string, List<string>> customHeaders = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            ArgumentNullException.ThrowIfNull(meetingId);

            // Construct URL
            var url = "v1/meetings/{meetingId}";
            url = url.Replace("{meetingId}", System.Uri.EscapeDataString(meetingId));

            return await GetResponseAsync<MeetingInfo>("FetchMeetingInfo", url, HttpMethod.Get, customHeaders: customHeaders, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
#pragma warning disable CA1801 // Review unused parameters - cannot change without breaking backwards compat.
        public async Task<TeamsMeetingParticipant> FetchParticipantAsync(string meetingId, string participantId, string tenantId, Dictionary<string, List<string>> customHeaders = null, CancellationToken cancellationToken = default(CancellationToken))
#pragma warning restore CA1801 // Review unused parameters
        {
            ArgumentNullException.ThrowIfNull(meetingId);
            ArgumentNullException.ThrowIfNull(participantId);
            ArgumentNullException.ThrowIfNull(tenantId);

            var content = new
            {
                meetingId,
                participantId,
                tenantId,
            };

            // Construct URL
            var url = "v1/meetings/{meetingId}/participants/{participantId}?tenantId={tenantId}";
            url = url.Replace("{meetingId}", System.Uri.EscapeDataString(meetingId));
            url = url.Replace("{participantId}", System.Uri.EscapeDataString(participantId));
            url = url.Replace("{tenantId}", System.Uri.EscapeDataString(tenantId));

            return await GetResponseAsync<TeamsMeetingParticipant>("FetchParticipant", url, HttpMethod.Get, customHeaders: customHeaders, cancellationToken: cancellationToken).ConfigureAwait(false); 
        }

        /// <inheritdoc/>
        public async Task<MeetingNotificationResponse> SendMeetingNotificationAsync(string meetingId, MeetingNotificationBase notification, Dictionary<string, List<string>> customHeaders = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            ArgumentNullException.ThrowIfNull(meetingId);

            // Construct URL
            var url = "v1/meetings/{meetingId}/notification";
            url = url.Replace("{meetingId}", Uri.EscapeDataString(meetingId));

            return await GetResponseAsync<MeetingNotificationResponse>("SendMeetingNotification", url, HttpMethod.Post, body: notification, customHeaders: customHeaders, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<string> SendMessageToListOfUsersAsync(IActivity activity, List<TeamMember> teamsMembers, string tenantId, Dictionary<string, List<string>> customHeaders = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            ArgumentNullException.ThrowIfNull(activity);
            ArgumentNullException.ThrowIfNullOrEmpty(tenantId);

            if (teamsMembers.Count == 0)
            {
                throw new ArgumentNullException(nameof(teamsMembers));
            }

            var content = new
            {
                Members = teamsMembers,
                Activity = activity,
                TenantId = tenantId,
            };

            var apiUrl = "v3/batch/conversation/users/";

            // In case of throttling, it will retry the operation with default values (10 retries every 50 miliseconds).
            var result = await RetryAction.RunAsync(
                task: () => GetResponseAsync<string>("SendMessageToListOfUsers", apiUrl, HttpMethod.Post, body: content, customHeaders: customHeaders, cancellationToken: cancellationToken),
                retryExceptionHandler: (ex, ct) => HandleThrottlingException(ex, ct)).ConfigureAwait(false);

            return result;
        }

        /// <inheritdoc/>
        public async Task<string> SendMessageToAllUsersInTenantAsync(IActivity activity, string tenantId, Dictionary<string, List<string>> customHeaders = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            ArgumentNullException.ThrowIfNull(activity);
            ArgumentNullException.ThrowIfNullOrEmpty(tenantId);

            var content = new
            {
                Activity = activity,
                TenantId = tenantId,
            };

            var apiUrl = "v3/batch/conversation/tenant/";

            // In case of throttling, it will retry the operation with default values (10 retries every 50 miliseconds).
            var result = await RetryAction.RunAsync(
                task: () => GetResponseAsync<string>("SendMessageToAllUsersInTenant", apiUrl, HttpMethod.Post, body: content, customHeaders: customHeaders, cancellationToken: cancellationToken),
                retryExceptionHandler: (ex, ct) => HandleThrottlingException(ex, ct)).ConfigureAwait(false);

            return result;
        }

        /// <inheritdoc/>
        public async Task<string> SendMessageToAllUsersInTeamAsync(IActivity activity, string teamId, string tenantId, Dictionary<string, List<string>> customHeaders = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            ArgumentNullException.ThrowIfNull(activity);
            ArgumentNullException.ThrowIfNullOrEmpty(teamId);
            ArgumentNullException.ThrowIfNullOrEmpty(tenantId);

            var content = new
            {
                Activity = activity,
                TeamId = teamId,
                TenantId = tenantId,
            };
     
            var apiUrl = "v3/batch/conversation/team/";

            // In case of throttling, it will retry the operation with default values (10 retries every 50 miliseconds).
            var result = await RetryAction.RunAsync(
                task: () => GetResponseAsync<string>("SendMessageToAllUsersInTeam", apiUrl, HttpMethod.Post, body: content, customHeaders: customHeaders, cancellationToken: cancellationToken),
                retryExceptionHandler: (ex, ct) => HandleThrottlingException(ex, ct)).ConfigureAwait(false);

            return result;
        }

        /// <inheritdoc/>
        public async Task<string> SendMessageToListOfChannelsAsync(IActivity activity, List<TeamMember> channelsMembers, string tenantId, Dictionary<string, List<string>> customHeaders = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            ArgumentNullException.ThrowIfNull(activity);

            if (channelsMembers.Count == 0)
            {
                throw new ArgumentNullException(nameof(channelsMembers));
            }

            if (string.IsNullOrEmpty(tenantId))
            {
                throw new ArgumentNullException(nameof(tenantId));
            }

            var content = new
            {
                Members = channelsMembers,
                Activity = activity,
                TenantId = tenantId,
            };

            var apiUrl = "v3/batch/conversation/channels/";

            // In case of throttling, it will retry the operation with default values (10 retries every 50 miliseconds).
            var result = await RetryAction.RunAsync(
                task: () => GetResponseAsync<string>("SendMessageToListOfChannels", apiUrl, HttpMethod.Post, body: content, customHeaders: customHeaders, cancellationToken: cancellationToken),
                retryExceptionHandler: (ex, ct) => HandleThrottlingException(ex, ct)).ConfigureAwait(false);

            return result;
        }

        /// <inheritdoc/>
        public async Task<BatchOperationState> GetOperationStateAsync(string operationId, Dictionary<string, List<string>> customHeaders = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            ArgumentNullException.ThrowIfNullOrEmpty(operationId);

            var apiUrl = "v3/batch/conversation/{operationId}";
            apiUrl = apiUrl.Replace("{operationId}", Uri.EscapeDataString(operationId));

            // In case of throttling, it will retry the operation with default values (10 retries every 50 miliseconds).
            var result = await RetryAction.RunAsync(
                task: () => GetResponseAsync<BatchOperationState>("GetOperationState", apiUrl, HttpMethod.Post, customHeaders: customHeaders, cancellationToken: cancellationToken),
                retryExceptionHandler: (ex, ct) => HandleThrottlingException(ex, ct)).ConfigureAwait(false);

            return result;
        }

        /// <inheritdoc/>
        public async Task<BatchFailedEntriesResponse> GetPagedFailedEntriesAsync(string operationId, Dictionary<string, List<string>> customHeaders = null, string continuationToken = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            ArgumentNullException.ThrowIfNullOrEmpty(operationId);

            var apiUrl = "v3/batch/conversation/failedentries/{operationId}";
            apiUrl = apiUrl.Replace("{operationId}", Uri.EscapeDataString(operationId));

            // In case of throttling, it will retry the operation with default values (10 retries every 50 miliseconds).
            var result = await RetryAction.RunAsync(
                task: () => GetResponseAsync<BatchFailedEntriesResponse>("GetPagedFailedEntries", apiUrl, HttpMethod.Get, continuationToken: continuationToken, customHeaders: customHeaders, cancellationToken: cancellationToken),
                retryExceptionHandler: (ex, ct) => HandleThrottlingException(ex, ct)).ConfigureAwait(false);

            return result;
        }

        /// <inheritdoc/>
        public async Task CancelOperationAsync(string operationId, Dictionary<string, List<string>> customHeaders = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            ArgumentNullException.ThrowIfNullOrEmpty(operationId);

            var apiUrl = "v3/batch/conversation/{operationId}";
            apiUrl = apiUrl.Replace("{operationId}", Uri.EscapeDataString(operationId));

            // In case of throttling, it will retry the operation with default values (10 retries every 50 miliseconds).
            await RetryAction.RunAsync(
                task: () => GetResponseAsync<BatchOperationState>("CancelOperation", apiUrl, HttpMethod.Delete, customHeaders: customHeaders, cancellationToken: cancellationToken),
                retryExceptionHandler: (ex, ct) => HandleThrottlingException(ex, ct)).ConfigureAwait(false);
        }

        private static RetryParams HandleThrottlingException(Exception ex, int currentRetryCount)
        {
            if (ex is ThrottleException throttlException)
            {
                return throttlException.RetryParams ?? RetryParams.DefaultBackOff(currentRetryCount);
            }
            else
            {
                return RetryParams.StopRetrying;
            }
        }

        private async Task<T> GetResponseAsync<T>(string operationName, string apiUrl, HttpMethod httpMethod, object body = null, string continuationToken = null, Dictionary<string, List<string>> customHeaders = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            var request = new HttpRequestMessage
            {
                Method = httpMethod,
                RequestUri = new Uri(Client.BaseUri, apiUrl)
                    .AppendQuery("continuationToken", continuationToken)
            };
            request.Headers.Add("Accept", "application/json");

            if (body != null)
            {
                request.Content = new StringContent(ProtocolJsonSerializer.ToJson(body), System.Text.Encoding.UTF8, "application/json");
            }

            if (customHeaders != null)
            {
                foreach (var customHeader in customHeaders)
                {
                    if (request.Headers.Contains(customHeader.Key))
                    {
                        request.Headers.Remove(customHeader.Key);
                    }

                    request.Headers.Add(customHeader.Key, customHeader.ToString());
                }
            }

            try
            {
                using var httpResponse = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                switch ((int)httpResponse.StatusCode)
                {
                    case 200:
                    case 201:
                        {
                            return ProtocolJsonSerializer.ToObject<T>(httpResponse.Content.ReadAsStream(cancellationToken));
                        }
                    case 429:
                        {
                            throw new ThrottleException() { RetryParams = currentRetryPolicy };
                        }
                    default:
                        {
                            var ex = new ErrorResponseException($"{operationName} operation returned an invalid status code '{httpResponse.StatusCode}'");
                            try
                            {
                                ErrorResponse errorBody = ProtocolJsonSerializer.ToObject<ErrorResponse>(httpResponse.Content.ReadAsStream(cancellationToken));
                                if (errorBody != null)
                                {
                                    ex.Body = errorBody;
                                }
                            }
                            catch (JsonException)
                            {
                                // Ignore the exception
                            }
                            throw ex;
                        }
                }
            }
            finally
            {
                // This means the request was successful. We can make our retry policy null.
                if (currentRetryPolicy != null)
                {
                    currentRetryPolicy = null;
                }
            }
        }
    }
}
