﻿using Microsoft.Agents.Extensions.Teams.AI.Models;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Extensions.Teams.AI.Tokenizers;
using Microsoft.Agents.Extensions.Teams.AI.State;
using Microsoft.Agents.Builder.State;

namespace Microsoft.Agents.Extensions.Teams.AI.Prompts.Sections
{
    /// <summary>
    /// A section that renders the conversation history.
    /// </summary>
    public class ConversationHistorySection : PromptSection
    {
        /// <summary>
        /// Name of memory variable used to store the histories.
        /// </summary>
        public readonly string Variable;

        /// <summary>
        /// Prefix to use for user messages when rendering as text. Defaults to `user: `.
        /// </summary>
        public readonly string UserPrefix;

        /// <summary>
        /// Prefix to use for assistant messages when rendering as text. Defaults to `assistant: `.
        /// </summary>
        public readonly string AssistantPrefix;

        /// <summary>
        /// Creates a new 'ConversationHistorySection' instance.
        /// </summary>
        /// <param name="variable">Name of memory variable used to store the histories.</param>
        /// <param name="tokens">Sizing strategy for this section. Defaults to `proportional` with a value of `1.0`.</param>
        /// <param name="required">Indicates if this section is required. Defaults to `false`.</param>
        /// <param name="userPrefix">Prefix to use for user messages when rendering as text. Defaults to `user: `.</param>
        /// <param name="assistantPrefix">Prefix to use for assistant messages when rendering as text. Defaults to `assistant: `.</param>
        /// <param name="separator">Separator to use between sections when rendering as text. Defaults to `\n`.</param>
        public ConversationHistorySection(string variable, int tokens = 1, bool required = false, string userPrefix = "user: ", string assistantPrefix = "assistant: ", string separator = "\n") : base(tokens, required, separator)
        {
            this.Variable = variable;
            this.UserPrefix = userPrefix;
            this.AssistantPrefix = assistantPrefix;
        }

        /// <inheritdoc />
        public override async Task<RenderedPromptSection<string>> RenderAsTextAsync(ITurnContext context, ITurnState memory, IPromptFunctions<List<string>> functions, ITokenizer tokenizer, int maxTokens, CancellationToken cancellationToken = default)
        {
            List<ChatMessage>? messages = memory.GetValue<List<ChatMessage>>(this.Variable);

            if (messages == null)
            {
                return new("", 0, false);
            }

            messages.Reverse();

            // Populate history and stay under the token budget

            int tokens = 0;
            int budget = this.Tokens > 1 ? Math.Min(this.Tokens, maxTokens) : maxTokens;
            int separatorLength = tokenizer.Encode(this.Separator).Count;
            List<string> lines = new();

            foreach (ChatMessage message in messages)
            {
                string prefix = message.Role == ChatRole.User ? UserPrefix : message.Role == ChatRole.Assistant ? AssistantPrefix : "";
                string line = prefix + message.Content;
                int length = tokenizer.Encode(line).Count;

                if (lines.Count > 0)
                {
                    length += separatorLength;
                }

                // Stop if we're over the token budget
                if (tokens + length > budget)
                {
                    break;
                }

                tokens += length;
                lines.Add(line);
            }

            string text = string.Join(this.Separator, lines);
            return await Task.FromResult(new RenderedPromptSection<string>(text, tokens, tokens > budget));
        }

        /// <inheritdoc />
        public override async Task<RenderedPromptSection<List<ChatMessage>>> RenderAsMessagesAsync(ITurnContext context, ITurnState memory, IPromptFunctions<List<string>> functions, ITokenizer tokenizer, int maxTokens, CancellationToken cancellationToken = default)
        {
            List<ChatMessage>? messages = memory.GetValue<List<ChatMessage>?>(this.Variable);

            if (messages == null)
            {
                return new(new() { }, 0, false);
            }

            // Shallow copy
            List<ChatMessage> history = new(messages);
            history.Reverse();

            // Populate history and stay under the token budget
            int tokens = 0;
            int budget = this.Tokens > 1 ? Math.Min(this.Tokens, maxTokens) : maxTokens;
            List<ChatMessage> output = new();

            foreach (ChatMessage message in history)
            {
                int length = tokenizer.Encode(GetMessageText(message)).Count;

                // Add length of any image parts
                // This accounts for low detail images but not high detail images.
                if (message.Content is IList<MessageContentParts> contentParts)
                {
                    length += contentParts.Where((part) => part is ImageContentPart).Count() * 85;
                }

                // Stop if we're over the token budget
                if (tokens + length > budget)
                {
                    break;
                }

                tokens += length;
                output.Insert(0, message);
            }

            // Remove completed partial action outputs
            while (messages.Count > 0 && messages[0].Role == ChatRole.Tool) 
            {
                messages.RemoveAt(0); 
            }

            return await Task.FromResult(new RenderedPromptSection<List<ChatMessage>>(output, tokens, tokens > budget));
        }
    }
}
