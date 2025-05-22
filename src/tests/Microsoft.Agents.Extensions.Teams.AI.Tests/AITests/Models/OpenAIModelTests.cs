﻿using Microsoft.Agents.Builder;
using Microsoft.Agents.Extensions.Teams.AI.Models;
using Microsoft.Agents.Extensions.Teams.AI.Prompts;
using Microsoft.Agents.Extensions.Teams.AI.Prompts.Sections;
using Microsoft.Agents.Extensions.Teams.AI.Tokenizers;
using Microsoft.Agents.Extensions.Teams.AI.Tests.TestUtils;
using Moq;
using OpenAI;
using OpenAI.Chat;
using OAIChatMessage = OpenAI.Chat.ChatMessage;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Reflection;
using ChatMessage = Microsoft.Agents.Extensions.Teams.AI.Models.ChatMessage;
using ChatRole = Microsoft.Agents.Extensions.Teams.AI.Models.ChatRole;
using Azure.Identity;
using Microsoft.Agents.Extensions.Teams.AI.Augmentations;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Extensions.Teams.AI.Tests.AITests.Models
{
    public class OpenAIModelTests
    {
        [Fact]
        public void Test_Constructor_OpenAI()
        {
            // Arrange
            var options = new OpenAIModelOptions("test-key", "test-model");

            // Act
            new OpenAIModel(options);
        }

        [Fact]
        public void Test_SetMaxTokens()
        {
            // Arrange
            var options = new OpenAIModelOptions("test-key", "test-model");
            var chatCompletionOptions = new ChatCompletionOptions();
            var model = new OpenAIModel(options);
            var testTokens = 100;

            // Act
            model.SetMaxTokens(testTokens, chatCompletionOptions);

            // Assert
            MethodInfo info = chatCompletionOptions.GetType().GetMethod("get__deprecatedMaxTokens", BindingFlags.NonPublic | BindingFlags.Instance)!;
            int maxTokens = (int)info.Invoke(chatCompletionOptions, null)!;
            Assert.Equal(testTokens, maxTokens);
        }


        [Fact]
        public void Test_Constructor_AzureOpenAI_InvalidAzureApiVersion()
        {
            // Arrange
            var options = new AzureOpenAIModelOptions("test-key", "test-deployment", "https://test.openai.azure.com/");
            var versions = new List<string>
            {
                "2024-06-01", "2024-10-21"
            };

            // Act
            foreach (var version in versions)
            {
                options.AzureApiVersion = version;
                new OpenAIModel(options);
            }
            options.AzureApiVersion = "2023-12-01-preview";
            Exception exception = Assert.Throws<ArgumentException>(() => new OpenAIModel(options));

            // Assert
            Assert.Equal("Model created with an unsupported API version of `2023-12-01-preview`.", exception.Message);
        }

        [Fact]
        public void Test_Constructor_AzureOpenAI_ManagedIdentityAuth()
        {
            // Arrange
            var options = new AzureOpenAIModelOptions(new DefaultAzureCredential(), "test-deployment", "https://test.openai.azure.com/");

            // Act
            new OpenAIModel(options);
        }

        [Fact]
        public async Task Test_CompletePromptAsync_AzureOpenAI_Chat_PromptTooLong()
        {
            // Arrange
            var turnContextMock = new Mock<ITurnContext>();
            var turnStateMock = new Mock<TurnState>();
            var renderedPrompt = new RenderedPromptSection<List<ChatMessage>>(new List<ChatMessage>(), length: 65536, tooLong: true);
            var promptMock = new Mock<Prompt>(new List<PromptSection>(), -1, true, "\n\n");
            promptMock.Setup((prompt) => prompt.RenderAsMessagesAsync(
                It.IsAny<ITurnContext>(), It.IsAny<TurnState>(), It.IsAny<IPromptFunctions<List<string>>>(),
                It.IsAny<ITokenizer>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(renderedPrompt);
            var promptTemplate = new PromptTemplate("test-prompt", promptMock.Object);
            var options = new AzureOpenAIModelOptions("test-key", "test-deployment", "https://test.openai.azure.com/")
            {
                CompletionType = CompletionConfiguration.CompletionType.Chat,
                LogRequests = true
            };
            var openAIModel = new OpenAIModel(options, loggerFactory: new TestLoggerFactory());

            // Act
            var result = await openAIModel.CompletePromptAsync(turnContextMock.Object, turnStateMock.Object, new PromptManager(), new GPTTokenizer(), promptTemplate);

            // Assert
            Assert.Equal(PromptResponseStatus.TooLong, result.Status);
            Assert.NotNull(result.Error);
            Assert.Equal("The generated chat completion prompt had a length of 65536 tokens which exceeded the MaxInputTokens of 2048.", result.Error.Message);
        }

        [Fact]
        public async Task Test_CompletePromptAsync_AzureOpenAI_Chat_RateLimited()
        {
            // Arrange
            var turnContextMock = new Mock<ITurnContext>();
            var turnStateMock = new Mock<TurnState>();
            var renderedPrompt = new RenderedPromptSection<List<ChatMessage>>(new List<ChatMessage>(), length: 256, tooLong: false);
            var promptMock = new Mock<Prompt>(new List<PromptSection>(), -1, true, "\n\n");
            promptMock.Setup((prompt) => prompt.RenderAsMessagesAsync(
                            It.IsAny<ITurnContext>(), It.IsAny<TurnState>(), It.IsAny<IPromptFunctions<List<string>>>(),
                            It.IsAny<ITokenizer>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(renderedPrompt);
            var promptTemplate = new PromptTemplate("test-prompt", promptMock.Object);
            var options = new AzureOpenAIModelOptions("test-key", "test-deployment", "https://test.openai.azure.com/")
            {
                CompletionType = CompletionConfiguration.CompletionType.Chat,
                LogRequests = true
            };
            var clientMock = new Mock<OpenAIClient>();
            var response = new TestResponse(429, "exception");
            var exception = new ClientResultException(response);
            clientMock.Setup((client) => client.GetChatClient(It.IsAny<string>()).CompleteChatAsync(It.IsAny<IEnumerable<OAIChatMessage>>(), It.IsAny<ChatCompletionOptions>(), It.IsAny<CancellationToken>())).ThrowsAsync(exception);
            var openAIModel = new OpenAIModel(options, loggerFactory: new TestLoggerFactory());
            openAIModel.GetType().GetField("_openAIClient", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(openAIModel, clientMock.Object);

            // Act
            var result = await openAIModel.CompletePromptAsync(turnContextMock.Object, turnStateMock.Object, new PromptManager(), new GPTTokenizer(), promptTemplate);

            // Assert
            Assert.Equal(PromptResponseStatus.RateLimited, result.Status);
            Assert.NotNull(result.Error);
            Assert.Equal("The chat completion API returned a rate limit error.", result.Error.Message);
        }

        [Fact]
        public async Task Test_CompletePromptAsync_AzureOpenAI_Chat_RequestFailed()
        {
            // Arrange
            var turnContextMock = new Mock<ITurnContext>();
            var turnStateMock = new Mock<TurnState>();
            var renderedPrompt = new RenderedPromptSection<List<ChatMessage>>(new List<ChatMessage>(), length: 256, tooLong: false);
            var promptMock = new Mock<Prompt>(new List<PromptSection>(), -1, true, "\n\n");
            promptMock.Setup((prompt) => prompt.RenderAsMessagesAsync(
                            It.IsAny<ITurnContext>(), It.IsAny<TurnState>(), It.IsAny<IPromptFunctions<List<string>>>(),
                            It.IsAny<ITokenizer>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(renderedPrompt);
            var promptTemplate = new PromptTemplate("test-prompt", promptMock.Object);
            var options = new AzureOpenAIModelOptions("test-key", "test-deployment", "https://test.openai.azure.com/")
            {
                CompletionType = CompletionConfiguration.CompletionType.Chat,
                LogRequests = true,
            };
            var clientMock = new Mock<OpenAIClient>();
            var response = new TestResponse(500, "exception");
            var exception = new ClientResultException(response);
            clientMock.Setup((client) => client.GetChatClient(It.IsAny<string>()).CompleteChatAsync(It.IsAny<IEnumerable<OAIChatMessage>>(), It.IsAny<ChatCompletionOptions>(), It.IsAny<CancellationToken>())).ThrowsAsync(exception);
            var openAIModel = new OpenAIModel(options, loggerFactory: new TestLoggerFactory());
            openAIModel.GetType().GetField("_openAIClient", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(openAIModel, clientMock.Object);

            // Act
            var result = await openAIModel.CompletePromptAsync(turnContextMock.Object, turnStateMock.Object, new PromptManager(), new GPTTokenizer(), promptTemplate);

            // Assert
            Assert.Equal(PromptResponseStatus.Error, result.Status);
            Assert.NotNull(result.Error);
            Assert.StartsWith("The chat completion API returned an error status of InternalServerError", result.Error.Message);
        }

        [Fact]
        public async Task Test_CompletePromptAsync_AzureOpenAI_Chat()
        {
            // Arrange
            var turnContextMock = new Mock<ITurnContext>();
            var turnStateMock = new Mock<TurnState>();
            var renderedPrompt = new RenderedPromptSection<List<ChatMessage>>(new List<ChatMessage>(), length: 256, tooLong: false);
            var promptMock = new Mock<Prompt>(new List<PromptSection>(), -1, true, "\n\n");
            promptMock.Setup((prompt) => prompt.RenderAsMessagesAsync(
                            It.IsAny<ITurnContext>(), It.IsAny<TurnState>(), It.IsAny<IPromptFunctions<List<string>>>(),
                            It.IsAny<ITokenizer>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(renderedPrompt);
            var promptTemplate = new PromptTemplate("test-prompt", promptMock.Object);
            var options = new AzureOpenAIModelOptions("test-key", "test-deployment", "https://test.openai.azure.com/")
            {
                CompletionType = CompletionConfiguration.CompletionType.Chat,
                LogRequests = true,
            };
            var clientMock = new Mock<OpenAIClient>();
            var chatCompletion = ModelReaderWriter.Read<ChatCompletion>(BinaryData.FromString(@$"{{
                ""choices"": [
                    {{
                        ""finish_reason"": ""stop"",
                        ""message"": {{
                            ""role"": ""assistant"",
                            ""content"": ""test-choice""
                        }}
                    }}
                ]
            }}"));
            var response = new TestResponse(200, string.Empty);
            clientMock.Setup((client) =>
                client
                .GetChatClient(It.IsAny<string>())
                .CompleteChatAsync(It.IsAny<IEnumerable<OAIChatMessage>>(), It.IsAny<ChatCompletionOptions>(), It.IsAny<CancellationToken>())
            ).ReturnsAsync(ClientResult.FromValue(chatCompletion!, response));

            var openAIModel = new OpenAIModel(options, loggerFactory: new TestLoggerFactory());
            openAIModel.GetType().GetField("_openAIClient", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(openAIModel, clientMock.Object);

            // Act
            var result = await openAIModel.CompletePromptAsync(turnContextMock.Object, turnStateMock.Object, new PromptManager(), new GPTTokenizer(), promptTemplate);

            // Assert
            Assert.Equal(PromptResponseStatus.Success, result.Status);
            Assert.NotNull(result.Message);
            Assert.Null(result.Error);
            Assert.Equal(ChatRole.Assistant, result.Message.Role);
            Assert.Equal("test-choice", result.Message.Content);
        }

        [Fact]
        public async Task Test_CompletePromptAsync_AzureOpenAI_Chat_WithTools()
        {
            // Arrange
            var turnContextMock = new Mock<ITurnContext>();
            var turnStateMock = new Mock<TurnState>();
            var renderedPrompt = new RenderedPromptSection<List<ChatMessage>>(new List<ChatMessage>(), length: 256, tooLong: false);
            var promptMock = new Mock<Prompt>(new List<PromptSection>(), -1, true, "\n\n");
            promptMock.Setup((prompt) => prompt.RenderAsMessagesAsync(
                            It.IsAny<ITurnContext>(), It.IsAny<TurnState>(), It.IsAny<IPromptFunctions<List<string>>>(),
                            It.IsAny<ITokenizer>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(renderedPrompt);
            var promptTemplate = new PromptTemplate("test-prompt", promptMock.Object)
            {
                Actions = new List<ChatCompletionAction>() { new ChatCompletionAction() { Name = "testAction" } },
                Augmentation = new ToolsAugmentation(),
                Configuration = new PromptTemplateConfiguration()
                {
                    Augmentation = new AugmentationConfiguration()
                    {
                        Type = AugmentationType.Tools
                    }
                }
            };
            var options = new AzureOpenAIModelOptions("test-key", "test-deployment", "https://test.openai.azure.com/")
            {
                CompletionType = CompletionConfiguration.CompletionType.Chat,
                LogRequests = true,
            };
            var clientMock = new Mock<OpenAIClient>();
            var chatCompletion = ModelReaderWriter.Read<ChatCompletion>(BinaryData.FromString(@$"{{
                ""choices"": [
                    {{
                        ""finish_reason"": ""stop"",
                        ""message"": {{
                            ""role"": ""assistant"",
                            ""content"": null,
                            ""tool_calls"": [
                              {{
                                ""id"": ""call_abc123"",
                                ""type"": ""function"",
                                ""function"": {{
                                  ""name"": ""testAction"",
                                  ""arguments"": ""{{}}""
                                }}
                              }}
                            ]
                        }}
                    }}
                ]
            }}"));
            var response = new TestResponse(200, string.Empty);
            clientMock.Setup((client) =>
                client
                .GetChatClient(It.IsAny<string>())
                .CompleteChatAsync(It.IsAny<IEnumerable<OAIChatMessage>>(), It.IsAny<ChatCompletionOptions>(), It.IsAny<CancellationToken>())
            ).ReturnsAsync(ClientResult.FromValue(chatCompletion!, response));

            var openAIModel = new OpenAIModel(options, loggerFactory: new TestLoggerFactory());
            openAIModel.GetType().GetField("_openAIClient", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(openAIModel, clientMock.Object);

            // Act
            var result = await openAIModel.CompletePromptAsync(turnContextMock.Object, turnStateMock.Object, new PromptManager(), new GPTTokenizer(), promptTemplate);

            // Assert
            Assert.Equal(PromptResponseStatus.Success, result.Status);
            Assert.NotNull(result.Message);

            Assert.NotNull(result.Message.ActionCalls);
            Assert.Single(result.Message.ActionCalls);
            Assert.Equal("testAction", result.Message.ActionCalls[0].Function!.Name);
            
            Assert.Null(result.Error);
            Assert.Equal(ChatRole.Assistant, result.Message.Role);
            Assert.Null(result.Message.Content);
        }

        [Fact]
        public async Task Test_CompletePromptAsync_AzureOpenAI_Streaming()
        {
            // Mock IChannelAdapter
            var channelAdapterMock = new Mock<IChannelAdapter>();

            // Arrange
            ITurnContext turnContext = new TurnContext(channelAdapterMock.Object, new Activity(
                text: "hello",
                channelId: "channelId",
                recipient: new() { Id = "recipientId" },
                conversation: new() { Id = "conversationId" },
                from: new() { Id = "fromId" }
            ));
            var streamer = new Mock<IStreamingResponse>();
            var state = new TurnState();
            await state.LoadStateAsync(turnContext);
            state.SetValue("temp.streamer", streamer.Object);
            var renderedPrompt = new RenderedPromptSection<List<ChatMessage>>(new List<ChatMessage>(), length: 256, tooLong: false);
            var promptMock = new Mock<Prompt>(new List<PromptSection>(), -1, true, "\n\n");
            promptMock.Setup((prompt) => prompt.RenderAsMessagesAsync(
                            It.IsAny<ITurnContext>(), It.IsAny<TurnState>(), It.IsAny<IPromptFunctions<List<string>>>(),
                            It.IsAny<ITokenizer>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(renderedPrompt);
            var promptTemplate = new PromptTemplate("test-prompt", promptMock.Object);
            var options = new AzureOpenAIModelOptions("test-key", "test-deployment", "https://test.openai.azure.com/")
            {
                CompletionType = CompletionConfiguration.CompletionType.Chat,
                LogRequests = true,
                Stream = true,
            };
            var clientMock = new Mock<OpenAIClient>();
            var update = ModelReaderWriter.Read<StreamingChatCompletionUpdate>(BinaryData.FromString(@$"{{
                ""choices"": [
                    {{
                        ""finish_reason"": null,
                        ""delta"": {{
                            ""role"": ""assistant"",
                            ""content"": ""chunk one""
                        }}
                    }}
                ]
            }}"));

            TestAsyncCollectionResult<StreamingChatCompletionUpdate> updates = new(update!, Mock.Of<PipelineResponse>());

            var response = new TestResponse(200, string.Empty);
            clientMock.Setup((client) =>
                client
                .GetChatClient(It.IsAny<string>())
                .CompleteChatStreamingAsync(It.IsAny<IEnumerable<OAIChatMessage>>(), It.IsAny<ChatCompletionOptions>(), It.IsAny<CancellationToken>())
            ).Returns(ClientResult.FromValue(updates, response));

            var openAIModel = new OpenAIModel(options, loggerFactory: new TestLoggerFactory());
            openAIModel.GetType().GetField("_openAIClient", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(openAIModel, clientMock.Object);

            // Act
            var result = await openAIModel.CompletePromptAsync(turnContext, state, new PromptManager(), new GPTTokenizer(), promptTemplate);

            // Assert
            Assert.Equal(PromptResponseStatus.Success, result.Status);
            Assert.NotNull(result.Message);
            Assert.Null(result.Error);
            Assert.Equal(ChatRole.Assistant, result.Message.Role);
            Assert.Equal("chunk one", result.Message.Content);
        }

    }
}