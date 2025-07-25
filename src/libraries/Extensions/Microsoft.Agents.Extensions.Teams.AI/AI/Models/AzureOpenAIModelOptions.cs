﻿using Azure.Core;
using Microsoft.Agents.Extensions.Teams.AI.Utilities;

namespace Microsoft.Agents.Extensions.Teams.AI.Models
{
    /// <summary>
    /// Options for configuring an `OpenAIModel` to call an Azure OpenAI hosted model.
    /// </summary>
    public class AzureOpenAIModelOptions : BaseOpenAIModelOptions
    {
        /// <summary>
        /// API key to use when making requests to Azure OpenAI.
        /// </summary>
        public string? AzureApiKey { get; set; }

        /// <summary>
        /// The token credential to use when making requests to Azure OpenAI.
        /// </summary>
        public TokenCredential? TokenCredential { get; set; }

        /// <summary>
        /// Default name of the Azure OpenAI deployment (model) to use.
        /// </summary>
        public string AzureDefaultDeployment { get; set; }

        /// <summary>
        /// Deployment endpoint to use.
        /// </summary>
        public string AzureEndpoint { get; set; }

        /// <summary>
        /// Optional. Version of the API being called.
        /// </summary>
        public string? AzureApiVersion { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureOpenAIModelOptions"/> class.
        /// </summary>
        /// <param name="azureApiKey">API key to use when making requests to Azure OpenAI.</param>
        /// <param name="azureDefaultDeployment">Default name of the Azure OpenAI deployment (model) to use.</param>
        /// <param name="azureEndpoint">Deployment endpoint to use.</param>
        /// <exception cref="System.ArgumentException"></exception>
        public AzureOpenAIModelOptions(
            string azureApiKey,
            string azureDefaultDeployment,
            string azureEndpoint) : base()
        {
            Verify.ParamNotNull(azureApiKey);
            Verify.ParamNotNull(azureDefaultDeployment);
            Verify.ParamNotNull(azureEndpoint);

            azureEndpoint = azureEndpoint.Trim();

            this.AzureApiKey = azureApiKey;
            this.AzureDefaultDeployment = azureDefaultDeployment;
            this.AzureEndpoint = azureEndpoint;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureOpenAIModelOptions"/> class.
        /// </summary>
        /// <param name="tokenCredential">token credential</param>
        /// <param name="azureDefaultDeployment">the deployment name</param>
        /// <param name="azureEndpoint">azure endpoint</param>
        public AzureOpenAIModelOptions(TokenCredential tokenCredential, string azureDefaultDeployment, string azureEndpoint)
        {
            Verify.ParamNotNull(tokenCredential);
            Verify.ParamNotNull(azureDefaultDeployment);
            Verify.ParamNotNull(azureEndpoint);

            this.TokenCredential = tokenCredential;
            this.AzureDefaultDeployment = azureDefaultDeployment;
            this.AzureEndpoint = azureEndpoint;
        }
    }
}
