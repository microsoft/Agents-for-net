﻿using Microsoft.Extensions.Configuration;
using Microsoft.Agents.Extensions.Teams.AI.Embeddings;
using Microsoft.Agents.Extensions.Teams.AI.Tests.TestUtils;
using System.Reflection;
using Xunit.Abstractions;
using Microsoft.Extensions.Logging;

namespace Microsoft.Agents.Extensions.Teams.AI.Tests.IntegrationTests
{
    public sealed class OpenAIEmbeddingsTests
    {
        private readonly IConfigurationRoot _configuration;
        private readonly RedirectOutput _output;
        private readonly ILoggerFactory _loggerFactory;

        public OpenAIEmbeddingsTests(ITestOutputHelper output)
        {
            _output = new RedirectOutput(output);
            _loggerFactory = new TestLoggerFactory(_output);

            var currentAssemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            if (string.IsNullOrWhiteSpace(currentAssemblyDirectory))
            {
                throw new InvalidOperationException("Unable to determine current assembly directory.");
            }

            var directoryPath = Path.GetFullPath(Path.Combine(currentAssemblyDirectory, $"../../../IntegrationTests/"));
            var settingsPath = Path.Combine(directoryPath, "testsettings.json");

            _configuration = new ConfigurationBuilder()
                .AddJsonFile(path: settingsPath, optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .AddUserSecrets<OpenAIEmbeddingsTests>()
                .Build();
        }

        [Fact(Skip = "This test should only be run manually.")]
        public async Task Test_CreateEmbeddingsAsync_OpenAI()
        {
            // Arrange
            var config = _configuration.GetSection("OpenAI").Get<OpenAIConfiguration>();
            var options = new OpenAIEmbeddingsOptions(config?.ApiKey ?? throw new Exception("config Missing in Test_CreateEmbeddingsAsync_OpenAI"), config.EmbeddingModelId!);
            var embeddings = new OpenAIEmbeddings(options, _loggerFactory);
            var inputs = new List<string>()
            {
                "test-input1",
                "test-input2"
            };
            var dimension = config.EmbeddingModelId!.Equals("text-embedding-3-large") ? 3072 : 1536;

            // Act
            var result = await embeddings.CreateEmbeddingsAsync(inputs);

            // Assert
            Assert.Equal(EmbeddingsResponseStatus.Success, result.Status);
            Assert.NotNull(result.Output);
            Assert.Equal(2, result.Output.Count);
            Assert.Equal(dimension, result.Output[0].Length);
            Assert.Equal(dimension, result.Output[1].Length);
        }

        [Fact(Skip = "This test should only be run manually.")]
        public async Task Test_CreateEmbeddingsAsync_AzureOpenAI()
        {
            // Arrange
            var config = _configuration.GetSection("AzureOpenAI").Get<AzureOpenAIConfiguration>();
            var options = new AzureOpenAIEmbeddingsOptions(config?.ApiKey ?? throw new Exception("config Missing in AzureContentSafetyModerator_ReviewPlan"), config.EmbeddingModelId!, config.Endpoint);
            var embeddings = new OpenAIEmbeddings(options, _loggerFactory);
            var inputs = new List<string>()
            {
                "test-input1",
                "test-input2"
            };
            var dimension = config.EmbeddingModelId!.Equals("text-embedding-3-large") ? 3072 : 1536;

            // Act
            var result = await embeddings.CreateEmbeddingsAsync(inputs);

            // Assert
            Assert.Equal(EmbeddingsResponseStatus.Success, result.Status);
            Assert.NotNull(result.Output);
            Assert.Equal(2, result.Output.Count);
            Assert.Equal(dimension, result.Output[0].Length);
            Assert.Equal(dimension, result.Output[1].Length);
        }
    }
}
