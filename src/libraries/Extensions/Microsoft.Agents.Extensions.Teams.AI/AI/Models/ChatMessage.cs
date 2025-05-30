﻿using Azure.AI.OpenAI.Chat;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Teams.AI.Exceptions;
using Microsoft.Agents.Extensions.Teams.AI.Utilities;
using Microsoft.Agents.Extensions.Teams.AI.Utilities.JsonConverters;
using OpenAI.Chat;
using System.Text;
using System.Text.Json.Serialization;
using OAI = OpenAI;

namespace Microsoft.Agents.Extensions.Teams.AI.Models
{
    /// <summary>
    /// Represents a message that will be passed to the Chat Completions API
    /// </summary>
    [JsonConverter(typeof(ChatMessageJsonConverter))]
    public class ChatMessage
    {
        /// <summary>
        /// The role associated with this message payload.
        /// </summary>
        public ChatRole Role { get; set; }

        /// <summary>
        /// The text associated with this message payload.
        /// </summary>
        public object? Content;

        /// <summary>
        /// The name of the author of this message. `name` is required if role is `function`, and it should be the name of the
        /// function whose response is in the `content`. May contain a-z, A-Z, 0-9, and underscores, with a maximum length of
        /// 64 characters.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// The name and arguments of a function that should be called, as generated by the model.
        /// </summary>
        [Obsolete("Use ActionCalls instead")]
        public FunctionCall? FunctionCall { get; set; }

        /// <summary>
        /// The ID of the tool call resolved by the provided content. `toolCallId` is required if role is `tool`.
        /// </summary>
        [Obsolete("Use ActionCalldId instead.")]
        public string? ToolCallId { get; set; }

        /// <summary>
        /// The context used for this message.
        /// </summary>
        [JsonPropertyName("context")]
        public MessageContext? Context { get; set; }

        /// <summary>
        /// The tool calls generated by the model, such as function calls.
        /// </summary>
        [Obsolete("Use ActionCalls instead")]
        public IList<ChatCompletionsToolCall>? ToolCalls { get; set; }

        /// <summary>
        /// Attachments for the bot to send back.
        /// </summary>
        [JsonPropertyName("attachments")]
        public List<Attachment>? Attachments { get; set; }

        /// <summary>
        /// The tool calls generated by the model, such as function calls.
        /// </summary>
        [JsonPropertyName("actionCalls")]
        public List<ActionCall>? ActionCalls { get; set; }

        /// <summary>
        /// The ID of the tool call resolved by the provided content. `toolCallId` is required if role is `tool`.
        /// </summary>
        [JsonPropertyName("actionCallId")]
        public string? ActionCallId { get; set; }

        /// <summary>
        /// Gets the content with the given type.
        /// Will throw an exception if the content is not of the given type.
        /// </summary>
        /// <returns>The content.</returns>
        public TContent GetContent<TContent>()
        {
            return (TContent)Content!;
        }

        /// <summary>
        /// Initializes a new instance of ChatMessage.
        /// </summary>
        /// <param name="role"> The role associated with this message payload. </param>
        public ChatMessage(ChatRole role)
        {
            this.Role = role;
        }

        /// <summary>
        /// Initializes a new instance of ChatMessage using OpenAI.Chat.ChatCompletion.
        /// </summary>
        /// <param name="chatCompletion"></param>
        internal ChatMessage(ChatCompletion chatCompletion)
        {
            this.Role = ChatRole.Assistant;

            // If finish reason is `toolCall` then there won't be any content.
            if (chatCompletion.Content.Count > 0)
            {
                this.Content = chatCompletion.Content[0].Text;
            }

            if (chatCompletion.FunctionCall != null && chatCompletion.FunctionCall.FunctionName != string.Empty)
            {
                this.Name = chatCompletion.FunctionCall.FunctionName;
                this.FunctionCall = new FunctionCall(chatCompletion.FunctionCall.FunctionName, chatCompletion.FunctionCall.FunctionArguments.ToString());
            }

            if (chatCompletion.ToolCalls != null && chatCompletion.ToolCalls.Count > 0)
            {
                // Note: Replaced `ToolCalls` field.
                this.ActionCalls = new List<ActionCall>();
                foreach (ChatToolCall toolCall in chatCompletion.ToolCalls)
                {
                    this.ActionCalls.Add(new ActionCall(toolCall));
                }
            }

#pragma warning disable AOAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            ChatMessageContext? azureContext = chatCompletion.GetMessageContext();
#pragma warning restore AOAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            if (azureContext != null)
            {
                MessageContext? context = new(azureContext);
                if (context != null)
                {
                    this.Context = context;
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of ChatMessage using OpenAI.Chat.StreamingChatCompletionUpdate.
        /// </summary>
        /// <param name="streamingChatCompletionUpdate">The streaming chat completion update.</param>
        internal ChatMessage(StreamingChatCompletionUpdate streamingChatCompletionUpdate)
        {
            this.Role = ChatRole.Assistant;

            if (streamingChatCompletionUpdate.ContentUpdate.Count > 0)
            {
                this.Content = streamingChatCompletionUpdate.ContentUpdate[0].Text;
            }

            if (streamingChatCompletionUpdate.FunctionCallUpdate != null && streamingChatCompletionUpdate.FunctionCallUpdate.FunctionName != string.Empty)
            {
                this.Name = streamingChatCompletionUpdate.FunctionCallUpdate.FunctionName;
                this.FunctionCall = new FunctionCall(streamingChatCompletionUpdate.FunctionCallUpdate.FunctionName, streamingChatCompletionUpdate.FunctionCallUpdate.FunctionArgumentsUpdate.ToString());
            }

            if (streamingChatCompletionUpdate.ToolCallUpdates != null && streamingChatCompletionUpdate.ToolCallUpdates.Count > 0)
            {
                this.ActionCalls = new List<ActionCall>();
                foreach (StreamingChatToolCallUpdate toolCall in streamingChatCompletionUpdate.ToolCallUpdates)
                {
                    this.ActionCalls.Add(new ActionCall(toolCall));
                }
            }

#pragma warning disable AOAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            ChatMessageContext? azureContext = streamingChatCompletionUpdate.GetMessageContext();
#pragma warning restore AOAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            if (azureContext != null)
            {
                MessageContext? context = new(azureContext);
                if (context != null)
                {
                    this.Context = context;
                }
            }
        }

        internal OAI.Chat.ChatMessage ToOpenAIChatMessage()
        {
            Verify.NotNull(this.Role);

            ChatRole role = this.Role;
            OAI.Chat.ChatMessage? message = null;

            string? content = null;
            List<ChatMessageContentPart> contentItems = new();
            StringBuilder textContentBuilder = new();

            // Content is a text
            if (this.Content is string)
            {
                content = (string)this.Content;
                textContentBuilder.AppendLine(content);
            }
            else if (this.Content is IEnumerable<MessageContentParts> contentParts)
            {
                // Content is has multiple possibly multi-modal parts.
                foreach (MessageContentParts contentPart in contentParts)
                {
                    if (contentPart is TextContentPart textPart)
                    {
                        contentItems.Add(ChatMessageContentPart.CreateTextPart(textPart.Text));
                        textContentBuilder.AppendLine(textPart.Text);
                    }
                    else if (contentPart is ImageContentPart imagePart)
                    {
                        contentItems.Add(ChatMessageContentPart.CreateImagePart(new Uri(imagePart.ImageUrl)));
                    }
                }
            }
            // else if content is null, then it must be a tool message.

            string textContent = textContentBuilder.ToString().Trim();

            // Different roles map to different classes
            if (role == ChatRole.User)
            {
                UserChatMessage userMessage;
                if (content != null)
                {
                    userMessage = new(content);
                }
                else
                {
                    userMessage = new(contentItems);
                }

                if (this.Name != null)
                {
                    // TODO: Currently no way to set `ParticipantName` come and change it eventually.
                    // userMessage.ParticipantName = this.Name;
                }

                message = userMessage;
            }

            if (role == ChatRole.Assistant)
            {
                AssistantChatMessage assistantMessage;

                if (this.FunctionCall != null)
                {
                    ChatFunctionCall functionCall = new(this.FunctionCall.Name ?? "", BinaryData.FromString(this.FunctionCall.Arguments ?? ""));
                    assistantMessage = new AssistantChatMessage(functionCall);
                }
                else if (this.ActionCalls != null)
                {
                    List<ChatToolCall> toolCalls = new();
                    foreach (ActionCall actionCall in this.ActionCalls)
                    {
                        toolCalls.Add(actionCall.ToChatToolCall());
                    }
                    assistantMessage = new AssistantChatMessage(toolCalls);
                }
                else
                {
                    assistantMessage = new AssistantChatMessage(textContent);
                }

                if (this.Name != null)
                {
                    // TODO: Currently no way to set `ParticipantName` come and change it eventually.
                    // assistantMessage.ParticipantName = this.Name;
                }

                message = assistantMessage;
            }

            if (role == ChatRole.System)
            {
                SystemChatMessage systemMessage = new(textContent);

                if (this.Name != null)
                {
                    // TODO: Currently no way to set `ParticipantName` come and change it eventually.
                    // systemMessage.ParticipantName = chatMessage.Name;
                }

                message = systemMessage;
            }

            if (role == ChatRole.Function)
            {
                // TODO: Clean up
#pragma warning disable CS0618 // Type or member is obsolete
                message = new FunctionChatMessage(this.Name ?? "", textContent);
#pragma warning restore CS0618 // Type or member is obsolete
            }

            if (role == ChatRole.Tool)
            {

                message = new ToolChatMessage(this.ActionCallId ?? "", textContent);
            }

            if (message == null)
            {
                throw new TeamsAIException($"Invalid chat message role: {role}");
            }

            return message;
        }
    }

    /// <summary>
    /// The name and arguments of a function that should be called, as generated by the model.
    /// </summary>
    [Obsolete("Deprecated for ActionCall")]
    public class FunctionCall
    {
        /// <summary>
        /// The name of the function to call.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The arguments to call the function with, as generated by the model in JSON format.
        /// Note that the model does not always generate valid JSON, and may hallucinate parameters
        /// not defined by your function schema. Validate the arguments in your code before calling
        /// your function.
        /// </summary>
        public string Arguments { get; set; } = string.Empty;

        /// <summary>
        /// Creates an instance of `FunctionCall`.
        /// </summary>
        /// <param name="name">function name</param>
        /// <param name="arguments">function arguments</param>
        public FunctionCall(string name, string arguments)
        {
            this.Name = name;
            this.Arguments = arguments;
        }
    }

    /// <summary>
    /// Action called by the model.
    /// </summary>
    public class ActionCall
    {
        /// <summary>
        /// The ID of the action call.
        /// </summary>
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        /// <summary>
        /// The function that the model called.
        /// </summary>
        [JsonPropertyName("function")]
        public ActionFunction? Function { get; set; }


        /// <summary>
        /// The type of the action. Currently, only "function" is supported.
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; } = ActionCallType.Function;

        /// <summary>
        /// Creates an instance of <see cref="ActionCall"/>
        /// </summary>
        /// <param name="id"></param>
        /// <param name="function"></param>
        public ActionCall(string id, ActionFunction function)
        {
            Id = id;
            Function = function;
        }

        /// <summary>
        /// Creates an instance of <see cref="ActionCall"/>.
        /// 
        /// Used to create the object when deserializing.
        /// </summary>
        [JsonConstructor]
        internal ActionCall() {}

        /// <summary>
        /// Creates an instance of <see cref="ActionCall"/> from <see cref="ChatToolCall"/>
        /// </summary>
        /// <param name="toolCall"></param>
        /// <exception cref="TeamsAIException">Thrown if `toolCall` has an invalid type</exception>
        public ActionCall(ChatToolCall toolCall)
        {
            if (toolCall.Kind != ChatToolCallKind.Function)
            {
                throw new TeamsAIException($"Invalid ActionCall type: {toolCall.GetType().Name}");
            }
            
            Id = toolCall.Id;
            Function = new ActionFunction(toolCall.FunctionName, toolCall.FunctionArguments.ToString());
        }

        /// <summary>
        /// Creates an instance of <see cref="ActionCall"/> from <see cref="StreamingChatToolCallUpdate"/>
        /// </summary>
        /// <param name="toolCall"></param>
        /// <exception cref="TeamsAIException">Thrown if `toolCall` has an invalid type</exception>
        public ActionCall(StreamingChatToolCallUpdate toolCall)
        {
            if (toolCall.Kind != ChatToolCallKind.Function)
            {
                throw new TeamsAIException($"Invalid ActionCall type: {toolCall.GetType().Name}");
            }

            Id = toolCall.ToolCallId;
            Function = new ActionFunction(toolCall.FunctionName, toolCall.FunctionArgumentsUpdate.ToString());
        }

        internal ChatToolCall ToChatToolCall()
        {
            if (this.Type == ActionCallType.Function)
            {
                return ChatToolCall.CreateFunctionToolCall(Id, Function!.Name, BinaryData.FromString(Function.Arguments));
            }

            throw new TeamsAIException($"Invalid tool type: {this.Type}");
        }
    }

    /// <summary>
    /// Represents an ActionCall type
    /// </summary>
    public class ActionCallType
    {
        /// <summary>
        /// Function action call type
        /// </summary>
        public static string Function { get; } = "function";
    }

    /// <summary>
    /// Function details associated with an action called by a model.
    /// </summary>
    public class ActionFunction
    {
        /// <summary>
        /// The name of the action to call.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The arguments to call the function with, as generated by the model in JSON format.
        /// Note that the model does not always generate valid JSON, and may hallucinate parameters
        /// not defined by your function schema. Validate the arguments in your code before calling
        /// your function.
        /// </summary>
        [JsonPropertyName("arguments")]
        public string Arguments { get; set; } = string.Empty;

        /// <summary>
        /// Creates an instance of `ActionFunction`.
        /// </summary>
        /// <param name="name">action name</param>
        /// <param name="arguments">function arguments</param>
        public ActionFunction(string name, string arguments)
        {
            this.Name = name;
            this.Arguments = arguments;
        }
    }

    /// <summary>
    /// Represents the ChatMessage content.
    /// </summary>
    public abstract class MessageContentParts
    {
        /// <summary>
        /// The type of message content.
        /// </summary>
        public string Type { get; }

        /// <summary>
        /// The chat message content.
        /// </summary>
        /// <param name="type"></param>
        public MessageContentParts(string type)
        {
            this.Type = type;
        }
    }

    /// <summary>
    /// The image content part of the ChatMessage
    /// </summary>
    public class TextContentPart : MessageContentParts
    {
        /// <summary>
        /// The constructor
        /// </summary>
        public TextContentPart() : base("text") { }

        /// <summary>
        /// The text of the message
        /// </summary>
        public string Text = string.Empty;
    }

    /// <summary>
    /// The image content part of the ChatMessage
    /// </summary>
    public class ImageContentPart : MessageContentParts
    {
        /// <summary>
        /// The constructor
        /// </summary>
        public ImageContentPart() : base("image") { }

        /// <summary>
        /// The URL of the image.
        /// </summary>
        public string ImageUrl = string.Empty;
    }
}
