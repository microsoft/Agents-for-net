// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Protocol;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Microsoft.Agents.Hosting.MCP
{
    public class MCPConverter
    {
        private static readonly JsonSerializerOptions s_ElementSerializerOptions = ProtocolJsonSerializer.SerializationOptions;

        public static async Task<T?> ReadRequestAsync<T>(HttpRequest request)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(request);
                return await JsonSerializer.DeserializeAsync<T>(request.Body, s_ElementSerializerOptions);
            }
            catch (JsonException)
            {
                return default;
            }
        }

        public static IActivity CreateActivityFromRequest(JsonRpcRequest jsonRpcPayload, string sessionId, string channelId = "mcp", bool isStreaming = true)
        {
            if (jsonRpcPayload.Params == null)
            {
                throw new ArgumentException("Params is null");
            }

            var message = JsonSerializer.Deserialize<ChatMessage>(jsonRpcPayload.Params["arguments"]["message"], s_ElementSerializerOptions);
            if (message == null)
            {
                throw new ArgumentException("Failed to parse request body");
            }

            var isIngress = true;
            return CreateActivity(message, jsonRpcPayload.Id.ToString(), sessionId, channelId, isIngress, isStreaming);
        }

        public static string CreateStreamMessageFromActivity(IActivity activity)
        {
            var chatUpdate = JsonSerializer.SerializeToNode(new ChatResponseUpdate(ChatRole.Tool, "hi back"), ProtocolJsonSerializer.SerializationOptions);
            return ProtocolJsonSerializer.ToJson(new JsonRpcResponse() { Id = new RequestId(activity.ReplyToId), Result = chatUpdate });
        }

        public static string ToJson(object obj)
        {
            return ProtocolJsonSerializer.ToJson(obj);
        }

        private static Activity CreateActivity(
            ChatMessage message,
            string requestId,
            string sessionId,
            string channelId, 
            bool isIngress,
            bool isStreaming = true)
        {
            var tool = new ChannelAccount
            {
                Id = "tool",
                Role = "tool",
            };

            var user = new ChannelAccount
            {
                Id = message.AuthorName ?? message.Role.Value,
                Role = message.Role.Value,
            };

            var activity = new Activity()
            {
                Type = ActivityTypes.Message,
                Id = Guid.NewGuid().ToString("N"),
                RequestId = requestId,
                ChannelId = channelId,
                DeliveryMode = isStreaming ? DeliveryModes.Stream : DeliveryModes.ExpectReplies,
                Conversation = new ConversationAccount
                {
                    Id = sessionId,
                },
                Recipient = isIngress ? tool : user,
                From = isIngress ? user : tool
            };

            foreach (var content in message.Contents)
            {
                if (content is TextContent tp)
                {
                    if (activity.Text == null)
                    {
                        activity.Text = tp.Text;
                    }
                    else
                    {
                        activity.Text += tp.Text;
                    }
                }
                else if (content is UriContent filePart)
                {
                    activity.Attachments.Add(new Attachment()
                    {
                        ContentType = filePart.MediaType,
                        ContentUrl = filePart.Uri.ToString(),
                    });
                }
                else if (content is DataContent dataPart)
                {
                    activity.Attachments.Add(new Attachment()
                    {
                        ContentType = dataPart.MediaType,
                        Content = dataPart.Base64Data,
                    });
                }
            }

            return activity;
        }

        public static JsonElement GetSchema(Type dataType)
        {
            JsonSchemaExporterOptions exporterOptions = new()
            {
                TreatNullObliviousAsNonNullable = true,
            };

            return JsonSerializer.Deserialize<JsonElement>(s_ElementSerializerOptions.GetJsonSchemaAsNode(dataType, exporterOptions));
        }
    }
}