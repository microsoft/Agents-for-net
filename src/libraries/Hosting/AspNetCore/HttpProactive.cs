// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.Proactive;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore
{
    internal class HttpProactive
    {
        public static async Task SendActivityWithConversationId<TAgent>(HttpRequest httpRequest, HttpResponse httpResponse, IChannelAdapter adapter, TAgent agent, string conversationId, CancellationToken cancellationToken) where TAgent : AgentApplication
        {
            await Execute<TAgent>(
                httpResponse, 
                async () =>
                {
                    var activity = await HttpHelper.ReadRequestAsync<IActivity>(httpRequest).ConfigureAwait(false);
                    if (activity == null)
                    {
                        return new Result(StatusCodes.Status400BadRequest, ErrorBody("Activity json body is required."));
                    }
                    return new Result(200, await agent.Proactive.SendActivityAsync(adapter, conversationId, activity, cancellationToken).ConfigureAwait(false));
                }, 
                (ex) =>
                {
                    if (ex is KeyNotFoundException)
                    {
                        return new Result(StatusCodes.Status404NotFound, ErrorBody($"Conversation with id '{conversationId}' not found."));
                    }
                    return null;
                },
                cancellationToken).ConfigureAwait(false);
        }

        public static async Task SendActivityWithConversation<TAgent>(HttpRequest httpRequest, HttpResponse httpResponse, IChannelAdapter adapter, TAgent agent, CancellationToken cancellationToken) where TAgent : AgentApplication
        {
            await Execute<TAgent>(
                httpResponse,
                async () =>
                {
                    var body = await HttpHelper.ReadRequestAsync<SendToConversationBody>(httpRequest).ConfigureAwait(false);

                    if (!body.Conversation.IsValid())
                    {
                        return new Result(StatusCodes.Status400BadRequest, ErrorBody("Conversation is invalid"));
                    }

                    if (body.Activity == null)
                    {
                        return new Result(StatusCodes.Status400BadRequest, ErrorBody("Activity is required."));
                    }

                    return new Result(StatusCodes.Status200OK, await Proactive.SendActivityAsync(adapter, body.Conversation, body.Activity, cancellationToken).ConfigureAwait(false));
                },
                null,
                cancellationToken).ConfigureAwait(false);
        }

        public static async Task ContinueConversationWithConversationId<TAgent>(ContinueConversationRoute<TAgent> continueRoute, HttpRequest httpRequest, HttpResponse httpResponse, IChannelAdapter adapter, TAgent agent, string conversationId, CancellationToken cancellationToken) where TAgent : AgentApplication
        {
            await Execute<TAgent>(
                httpResponse,
                async () =>
                {
                    var conversation = await agent.Proactive.GetConversationAsync(conversationId, cancellationToken).ConfigureAwait(false);
                    if (conversation == null)
                    {
                        return new Result(StatusCodes.Status404NotFound, ErrorBody($"Conversation with id '{conversationId}' not found."));
                    }

                    // Creating a continuation activity with Value containing Query args.
                    var continuationActivity = conversation.Reference.GetContinuationActivity();
                    var eventValue = httpRequest.Query.Select(p => KeyValuePair.Create(p.Key, p.Value.ToString())).ToDictionary();
                    if (eventValue.Count > 0)
                    {
                        continuationActivity.ValueType = Proactive.ContinueConversationValueType;
                        continuationActivity.Value = eventValue;
                    }

                    await agent.Proactive.ContinueConversationAsync(
                        adapter,
                        conversation,
                        continueRoute.RouteHandler(agent),
                        continuationActivity,
                        continueRoute.TokenHandlers,
                        cancellationToken).ConfigureAwait(false);

                    return new Result(StatusCodes.Status200OK);
                },
                (ex) =>
                {
                    if (ex is KeyNotFoundException)
                    {
                        return new Result(StatusCodes.Status404NotFound, ErrorBody($"Conversation with id '{conversationId}' not found."));
                    }
                    return null;
                },
                cancellationToken).ConfigureAwait(false);
        }

        public static async Task ContinueConversationWithConversation<TAgent>(ContinueConversationRoute<TAgent> continueRoute, HttpRequest httpRequest, HttpResponse httpResponse, IChannelAdapter adapter, TAgent agent, CancellationToken cancellationToken) where TAgent : AgentApplication
        {
            await Execute<TAgent>(
                httpResponse,
                async () =>
                {
                    var conversation = await HttpHelper.ReadRequestAsync<Conversation>(httpRequest).ConfigureAwait(false);
                    if (!conversation.IsValid())
                    {
                        return new Result(StatusCodes.Status400BadRequest, ErrorBody("Conversation is invalid"));
                    }

                    // Creating a continuation activity with Value containing Query args.
                    var continuationActivity = conversation.Reference.GetContinuationActivity();
                    var eventValue = httpRequest.Query.Select(p => KeyValuePair.Create(p.Key, p.Value.ToString())).ToDictionary();
                    if (eventValue.Count > 0)
                    {
                        continuationActivity.ValueType = Proactive.ContinueConversationValueType;
                        continuationActivity.Value = eventValue;
                    }

                    await agent.Proactive.ContinueConversationAsync(
                        adapter,
                        conversation,
                        continueRoute.RouteHandler(agent),
                        continuationActivity,
                        continueRoute.TokenHandlers,
                        cancellationToken).ConfigureAwait(false);

                    return new Result(StatusCodes.Status200OK);
                },
                null,
                cancellationToken).ConfigureAwait(false);
        }

        public static async Task CreateConversation<TAgent>(ContinueConversationRoute<TAgent> continueRoute, HttpRequest httpRequest, HttpResponse httpResponse, IChannelAdapter adapter, TAgent agent, CancellationToken cancellationToken) where TAgent : AgentApplication
        {
            await Execute<TAgent>(
                httpResponse,
                async () =>
                {
                    var body = await HttpHelper.ReadRequestAsync<CreateConversationBody>(httpRequest).ConfigureAwait(false);

                    // Create the CreateConversation instance from the request body.
                    IDictionary<string, string> claims;
                    if (string.IsNullOrWhiteSpace(body.AgentClientId))
                    {
                        claims = Conversation.ClaimsFromIdentity(HttpHelper.GetClaimsIdentity(httpRequest));
                    }
                    else
                    {
                        claims = new Dictionary<string, string>
                    {
                        { "aud", body.AgentClientId },
                    };
                    }

                    if (claims == null || claims.Count == 0)
                    {
                        return new Result(StatusCodes.Status400BadRequest, ErrorBody("AgentClientId or a valid JWT Bearer token is required."));
                    }

                    if (string.IsNullOrWhiteSpace(body.ChannelId))
                    {
                        return new Result(StatusCodes.Status400BadRequest, ErrorBody("ChannelId is required."));
                    }

                    var createRecordBuilder = CreateConversationBuilder.Create(claims, body.ChannelId)
                        .WithActivity(body.Activity)
                        .WithTopicName(body.TopicName)
                        .WithUser(body.User)
                        .WithChannelData(body.ChannelData)
                        .WithTeamsChannelId(body.TeamsChannelId)
                        .WithTenantId(body.TenantId);

                    if (body.IsGroup.HasValue)
                    {
                        createRecordBuilder.IsGroup(body.IsGroup.Value);
                    }
                        
                    var createRecord = createRecordBuilder.Build();

                    // Execute the conversation creation
                    var newReference = await agent.Proactive.CreateConversationAsync(
                        adapter,
                        createRecord,
                        body.ContinueConversation ? continueRoute.RouteHandler(agent) : null,
                        (reference) =>
                        {
                            var continuationActivity = reference.GetContinuationActivity();
                            var eventValue = httpRequest.Query.Select(p => KeyValuePair.Create(p.Key, p.Value.ToString())).ToDictionary();
                            if (eventValue.Count > 0)
                            {
                                continuationActivity.ValueType = Proactive.ContinueConversationValueType;
                                continuationActivity.Value = eventValue;
                            }
                            return continuationActivity;
                        },
                        continueRoute.TokenHandlers,
                        cancellationToken).ConfigureAwait(false);

                    // Store the conversation if requested, and return the Conversation in the response body.
                    var conversation = new Conversation(newReference, claims);

                    if (body.StoreConversation)
                    {
                        await agent.Proactive.StoreConversationAsync(conversation, cancellationToken).ConfigureAwait(false);
                    }

                    return new Result(StatusCodes.Status200OK, conversation);
                },
                null,
                cancellationToken).ConfigureAwait(false);
        }

        private static async Task Execute<TAgent>(HttpResponse httpResponse, Func<Task<Result>> action, Func<Exception, Result> exceptionHandler, CancellationToken cancellationToken) where TAgent : AgentApplication
        {
            try 
            {
                var result = await action().ConfigureAwait(false);
                httpResponse.StatusCode = result.StatusCode;
                if (result.Body != null)
                {
                    using var memoryStream = new MemoryStream(Encoding.ASCII.GetBytes(ProtocolJsonSerializer.ToJson(result.Body)));
                    httpResponse.Headers.ContentType = "application/json";
                    await memoryStream.CopyToAsync(httpResponse.Body, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (ErrorResponseException errorResponse)
            {
                httpResponse.StatusCode = (int)errorResponse.StatusCode.GetValueOrDefault(StatusCodes.Status500InternalServerError);
                if (errorResponse.Body != null)
                {
                    using var memoryStream = new MemoryStream(Encoding.ASCII.GetBytes(ProtocolJsonSerializer.ToJson(errorResponse.Body)));
                    httpResponse.Headers.ContentType = "application/json";
                    await memoryStream.CopyToAsync(httpResponse.Body, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when(ex is InvalidOperationException || ex is ArgumentException || ex is ArgumentNullException)
            {
                httpResponse.StatusCode = StatusCodes.Status400BadRequest;
                await httpResponse.WriteAsJsonAsync(new { error = new { message = ex.Message } }, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception requestFailed)
            {
                var result = exceptionHandler?.Invoke(requestFailed);
                if (result != null)
                {
                    httpResponse.StatusCode = result.StatusCode;
                    if (result.Body != null)
                    {
                        httpResponse.Headers.ContentType = "application/json";
                        using var memoryStream = new MemoryStream(Encoding.ASCII.GetBytes(ProtocolJsonSerializer.ToJson(result.Body)));
                        await memoryStream.CopyToAsync(httpResponse.Body, cancellationToken).ConfigureAwait(false);
                    }
                }
                else
                {
                    httpResponse.StatusCode = StatusCodes.Status500InternalServerError;
                    await httpResponse.WriteAsJsonAsync(new { error = new { message = requestFailed.Message } }, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private static object ErrorBody(string message)
        {
            return new { error = message };
        }
    }

    record Result(int StatusCode, object Body = null) {}

    class SendToConversationBody
    {
        public Conversation Conversation { get; set; }
        public IActivity Activity { get; set; } = default!;
    }

    class CreateConversationBody
    {
        public string AgentClientId { get; set; }
        public string ChannelId { get; set; }
        public bool? IsGroup { get; set; }
        public ChannelAccount User { get; set; }
        public string TopicName { get; set; }
        public string TenantId { get; set; }
        public IActivity Activity { get; set; }
        public string TeamsChannelId { get; set; }
        public object ChannelData { get; set; }
        public bool StoreConversation { get; set; } = false;
        public bool ContinueConversation { get; set; } = false;
    }
}
