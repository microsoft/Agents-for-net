// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Agents.Core.Models;
using System.Threading.Tasks;
using Microsoft.Agents.CopilotStudio.Client.Discovery;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Core;

[assembly: InternalsVisibleTo("Microsoft.Agents.CopilotStudio.Client.Tests, PublicKey=0024000004800000940000000602000000240000525341310004000001000100b5fc90e7027f67871e773a8fde8938c81dd402ba65b9201d60593e96c492651e889cc13f1415ebb53fac1131ae0bd333c5ee6021672d9718ea31a8aebd0da0072f25d87dba6fc90ffd598ed4da35e44c398c454307e8e33b8426143daec9f596836f97c8f74750e5975c64e2189f45def46b2a2b1247adc3652bf5c308055da9")]

namespace Microsoft.Agents.CopilotStudio.Client
{
    /// <summary>
    /// This Client is used to connect to DirectToEngine API endpoint for Copilot Studio.
    /// </summary>
    public class CopilotClient
    {
        /// <summary>
        /// Conversation ID being used for the current conversation.
        /// </summary>
        private string ConversationId = string.Empty;
        /// <summary>
        /// Content Type for Event Stream
        /// </summary>
        private static readonly MediaTypeWithQualityHeaderValue s_EventStream = new("text/event-stream");
        /// <summary>
        /// Content Type for Conversation Start
        /// </summary>
        private static readonly MediaTypeWithQualityHeaderValue s_ApplicationJson = new("application/json");
        /// <summary>
        /// Http Client Factory to use for connecting to Copilot Studio
        /// </summary>
        private IHttpClientFactory _httpClientFactory;
        /// <summary>
        /// Http Client name to use from the factory
        /// </summary>
        private string _httpClientName = string.Empty;
        /// <summary>
        /// ILogger to log events on for DirectToEngine operations.
        /// </summary>
        private ILogger _logger;
        /// <summary>
        /// Token Provider Function to get the access token for the request.
        /// </summary>
        private Func<string, Task<string>>? _tokenProviderFunction = null;
        /// <summary>
        /// Connection Settings for Copilot Studio
        /// </summary>
        public ConnectionSettings Settings;

        /// <summary>
        /// Returns the Scope URL need to connect to Copilot Studio from the Connection Settings
        /// </summary>
        /// <param name="settings">Copilot Studio Connection Settings</param>
        /// <returns></returns>
        public static string? ScopeFromSettings(ConnectionSettings settings) => PowerPlatformEnvironment.GetTokenAudience(settings);

        /// <summary>
        /// Creates a DirectToEngine client for Microsoft Copilot Studio hosted bots. 
        /// </summary>
        /// <param name="settings">Configuration Settings for Connecting to Copilot Studio</param>
        /// <param name="httpClientFactory">Http Client Factory to use when connecting to Copilot Studio</param>
        /// <param name="httpClientName">Name of HttpClient to use from the factory</param>
        /// <param name="logger">ILogger to log events on for DirectToEngine operations. </param>
        public CopilotClient(ConnectionSettings settings, IHttpClientFactory httpClientFactory, ILogger logger, string httpClientName = "mcs")
        {
            Settings = settings;
            _httpClientFactory = httpClientFactory;
            _httpClientName = httpClientName;
            _logger = logger;
        }


        /// <summary>
        /// Creates a DirectToEngine client for Microsoft Copilot Studio hosted bots. 
        /// </summary>
        /// <param name="settings">Configuration Settings for Connecting to Copilot Studio</param>
        /// <param name="httpClientFactory">Http Client Factory to use when connecting to Coilot Studio</param>
        /// <param name="logger">ILogger to log events on for DirectToEngine operations. </param>
        /// <param name="httpClientName">Name of HttpClient to use from the factory</param>
        /// <param name="tokenProviderFunction">Function pointer for a async function that will accept an URL and return an AccessToken</param>
        public CopilotClient(ConnectionSettings settings, IHttpClientFactory httpClientFactory, Func<string, Task<string>> tokenProviderFunction, ILogger logger, string httpClientName)
        {
            Settings = settings;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _httpClientName = httpClientName;
            _tokenProviderFunction = tokenProviderFunction;
        }

        /// <summary>
        /// Posts a request to Copilot Studio and returns the response as an IAsyncEnumerable of IActivity
        /// </summary>
        /// <param name="req">Request Object to send to Copilot Studio</param>
        /// <param name="ct">CancellationToken used to handle interruption request</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="HttpRequestException"></exception>
        private async IAsyncEnumerable<IActivity> PostRequestAsync(HttpRequestMessage req, [EnumeratorCancellation] CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(req);

            HttpClient? httpClient = null;
            if (string.IsNullOrEmpty(_httpClientName))
            {
                httpClient = _httpClientFactory.CreateClient(); // Get the default client. 
            }
            else
            {
                httpClient = _httpClientFactory.CreateClient(_httpClientName);
            }

            if (httpClient is null)
            {
                throw new ArgumentException("Unable to create a connection to Copilot Studio Server");
            }

            PropagateHeaders(httpClient);

            if (_tokenProviderFunction != null)
            {
                // Set the access token header when its provided via an external token provider. 
                // If not done here the expecation is that the Token will be provided by an httpclient handler.
                string accessToken = string.Empty;
                if (req?.RequestUri != null)
                {
                    accessToken = await _tokenProviderFunction(req.RequestUri.ToString());
                }
                else
                {
                    accessToken = await _tokenProviderFunction(string.Empty);
                }

                ArgumentNullException.ThrowIfNull(req); // Dealing with the compiler warning.
                if (!string.IsNullOrEmpty(accessToken))
                {
                    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                }
            }

            using HttpResponseMessage resp = await httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogError("Error sending request: {Status}", resp.StatusCode);
                if (resp.Content != null)
                {
                    string error = await resp.Content.ReadAsStringAsync(ct);
                    _logger.LogError("Error: {Error}", error);
                    throw new HttpRequestException($"Error sending request: {resp.StatusCode}. {error}");
                }
                throw new HttpRequestException($"Error sending request: {resp.StatusCode}");
            }
            else
            {
                _logger.LogInformation("Request sent successfully");
            }
            using Stream stream = await resp.Content.ReadAsStreamAsync(ct);
            using StreamReader sr = new(stream);
            string streamType = string.Empty;
            while (!sr.EndOfStream)
            {
                //JsonDocument jsonDoc = null!;
                string line = sr.ReadLine()!;
                if (line!.StartsWith("event:", StringComparison.InvariantCulture))
                {
                    streamType = line[7..];
                }
                else if (line.StartsWith("data:", StringComparison.InvariantCulture) && streamType == "activity")
                {
                    string jsonRaw = line[6..];
                    _logger.LogTrace(jsonRaw);
                    Activity activity = ProtocolJsonSerializer.ToObject<Activity>(jsonRaw);
                    switch (activity.Type)
                    {
                        case "message":
                            ConversationId = activity.Conversation.Id;
                            _logger.LogInformation("Conversation ID: {ConversationId}", ConversationId);
                            yield return activity;
                            break;
                        default:
                            yield return activity;
                            break;
                    }
                }
                else
                {
                    _logger.LogTrace(".");
                }
            }
        }

        /// <summary>
        /// Used to start a conversation with MCS. 
        /// </summary>
        /// <param name="emitStartConversationEvent">Should ask remote bot to emit start event</param>
        /// <param name="cancellationToken">Event Cancelation Token</param>
        /// <returns></returns>
        public IAsyncEnumerable<IActivity> StartConversationAsync(bool emitStartConversationEvent = true, CancellationToken cancellationToken = default)
        {
            using (_logger.BeginScope("D2E:StartConversationAsync"))
            {
                _logger.LogTrace("Starting conversation");
                Uri uriStart = PowerPlatformEnvironment.GetCopilotStudioConnectionUrl(Settings, null);
                var body = new { EmitStartConversationEvent = emitStartConversationEvent };

                HttpRequestMessage req = new()
                {
                    Method = HttpMethod.Post,
                    RequestUri = uriStart,
                    Headers =
                    {
                        Accept = { s_EventStream }
                    },
                    Content = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(Microsoft.Agents.Core.Serialization.ProtocolJsonSerializer.ToJson(body)))
                    {
                        Headers =
                        {
                            ContentType = s_ApplicationJson,
                        }
                    }
                };
                return PostRequestAsync(req, cancellationToken);
            }
        }

        /// <summary>
        /// Sends a String question to the remote bot and returns the response as an IAsyncEnumerable of IActivity
        /// </summary>
        /// <param name="question">String Question to send to copilot</param>
        /// <param name="conversationId">Conversation ID to reference, Optional. If not set it will pick up the current conversation id</param>
        /// <param name="ct">Event Cancelation Token</param>
        /// <returns></returns>
        public IAsyncEnumerable<IActivity> AskQuestionAsync(string question, string? conversationId = default, CancellationToken ct = default)
        {
            var activity = new Activity { 
                Type = "message",
                Text = question,
                Conversation = new ConversationAccount { Id = conversationId }
            };
            return AskQuestionAsync(activity, ct);
        }

        /// <summary>
        /// Sends an activity the remote bot and returns the response as an IAsyncEnumerable of IActivity
        /// </summary>
        /// <param name="activity" >Activity to send</param>
        /// <param name="ct">Event Cancelation Token</param>
        /// <returns></returns>
        public IAsyncEnumerable<IActivity> AskQuestionAsync(IActivity activity, CancellationToken ct = default)
        {
            using (_logger.BeginScope("D2E:AskQuestionAsync"))
            {
                ArgumentNullException.ThrowIfNull(activity);
                string localConversationId = "";
                if (!string.IsNullOrEmpty(activity.Conversation?.Id))
                    localConversationId = activity.Conversation.Id;
                else
                    localConversationId = ConversationId;

                Uri uriExecute = PowerPlatformEnvironment.GetCopilotStudioConnectionUrl(Settings, localConversationId);
                ExecuteTurnRequest qbody = new() { Activity =(Activity)activity };
                HttpRequestMessage qreq = new()
                {
                    Method = HttpMethod.Post,
                    RequestUri = uriExecute,
                    Headers =
                {
                    Accept = { s_EventStream }
                },
                    Content = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(Microsoft.Agents.Core.Serialization.ProtocolJsonSerializer.ToJson(qbody)))
                    {
                        Headers =
                    {
                        ContentType = s_ApplicationJson,
                    }
                    }
                };
                return PostRequestAsync(qreq, ct);
            }
        }

        private void PropagateHeaders(HttpClient httpClient)
        {
            IHeaderPropagation propagateHeaders = RequestContext.GetHeaderPropagation();
            if (propagateHeaders != null)
            {
                if (propagateHeaders.Headers != null)
                {
                    foreach (var header in propagateHeaders.Headers)
                    {
                        httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
                    }
                }
            }
        }
    }
}
