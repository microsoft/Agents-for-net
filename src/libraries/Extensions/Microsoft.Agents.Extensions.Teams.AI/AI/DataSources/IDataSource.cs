﻿using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Extensions.Teams.AI.Prompts.Sections;
using Microsoft.Agents.Extensions.Teams.AI.Tokenizers;
using Microsoft.Agents.Extensions.Teams.AI.State;

namespace Microsoft.Agents.Extensions.Teams.AI.DataSources
{
    /// <summary>
    /// A data source that can be used to render text that's added to a prompt.
    /// </summary>
    public interface IDataSource
    {
        /// <summary>
        /// Name of the data source.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Renders the data source as a string of text.
        /// </summary>
        /// <param name="context">Turn context for the current turn of conversation with the user.</param>
        /// <param name="memory">An interface for accessing state values.</param>
        /// <param name="tokenizer">Tokenizer to use when rendering the data source.</param>
        /// <param name="maxTokens">Maximum number of tokens allowed to be rendered.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns></returns>
        public Task<RenderedPromptSection<string>> RenderDataAsync(ITurnContext context, ITurnState memory, ITokenizer tokenizer, int maxTokens, CancellationToken cancellationToken = default);
    }
}
