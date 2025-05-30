﻿using Microsoft.Agents.Extensions.Teams.AI.Models;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Extensions.Teams.AI.DataSources;
using Microsoft.Agents.Extensions.Teams.AI.Tokenizers;
using Microsoft.Agents.Extensions.Teams.AI.State;
using Microsoft.Agents.Builder.State;

namespace Microsoft.Agents.Extensions.Teams.AI.Prompts.Sections
{
    /// <summary>
    /// A datasource section that will be rendered as a message
    /// </summary>
    public class DataSourceSection : PromptSection
    {
        private readonly IDataSource _source;

        /// <summary>
        /// Creates an instance of `DataSourceSection`
        /// </summary>
        /// <param name="dataSource">data source to render</param>
        /// <param name="tokens">number of tokens</param>
        public DataSourceSection(IDataSource dataSource, int tokens = -1) : base(tokens, true, "\n\n")
        {
            this._source = dataSource;
        }

        /// <inheritdoc />
        public override async Task<RenderedPromptSection<List<ChatMessage>>> RenderAsMessagesAsync(ITurnContext context, ITurnState memory, IPromptFunctions<List<string>> functions, ITokenizer tokenizer, int maxTokens, CancellationToken cancellationToken = default)
        {
            int budget = this.Tokens > 1 ? Math.Min(this.Tokens, maxTokens) : maxTokens;
            RenderedPromptSection<string> rendered = await this._source.RenderDataAsync(context, memory, tokenizer, budget, cancellationToken);
            List<ChatMessage> messages = new()
            {new(ChatRole.System) { Content = rendered.Output } };

            return new(messages, rendered.Length, rendered.TooLong);
        }
    }
}
