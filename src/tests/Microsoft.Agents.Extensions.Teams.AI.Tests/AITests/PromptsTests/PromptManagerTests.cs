﻿using Microsoft.Agents.Builder;
using Microsoft.Agents.Extensions.Teams.AI.Prompts;
using Microsoft.Agents.Extensions.Teams.AI.Tokenizers;
using Microsoft.Agents.Extensions.Teams.AI.State;
using Moq;
using System.Reflection;

namespace Microsoft.Agents.Extensions.Teams.AI.Tests.AITests.PromptsTests
{
    public class PromptManagerTests
    {
        [Fact]
        public async Task Test_Functions()
        {
            PromptManager manager = new();
            Mock<ITurnContext> context = new();
            MemoryFork memory = new();
            GPTTokenizer tokenizer = new();

            manager.AddFunction("getMessage", async (context, memory, functions, tokenizer, args) =>
            {
                return await Task.FromResult("Hello World!");
            });

            dynamic? result = await manager.InvokeFunctionAsync("getMessage", context.Object, memory, tokenizer, new() { });

            Assert.True(manager.HasFunction("getMessage"));
            Assert.NotNull(manager.GetFunction("getMessage"));
            Assert.Equal("Hello World!", result);
        }

        [Fact]
        public void Test_Prompts()
        {
            PromptManager manager = new();

            manager.AddPrompt("MyPrompt", new("MyPrompt", new(new() { }), new()));

            Assert.True(manager.HasPrompt("MyPrompt"));
            Assert.Equal("MyPrompt", manager.GetPrompt("MyPrompt").Name);
        }

        [Fact]
        public void Test_Prompts_LoadFromFileSystem()
        {
            var currentAssemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            if (string.IsNullOrWhiteSpace(currentAssemblyDirectory))
            {
                throw new InvalidOperationException("Unable to determine current assembly directory.");
            }

            PromptManager manager = new(new()
            {
                PromptFolder = Path.GetFullPath(Path.Combine(currentAssemblyDirectory, $"../../../../src/tests/Microsoft.Agents.Extensions.Teams.AI.Tests/AITests/prompts"))
            });

            PromptTemplate template = manager.GetPrompt("promptTemplateFolder");

            Assert.NotNull(template);
        }

        [Fact]
        public void Test_Prompts_LoadFromFileSystem_NotFound()
        {
            var currentAssemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            if (string.IsNullOrWhiteSpace(currentAssemblyDirectory))
            {
                throw new InvalidOperationException("Unable to determine current assembly directory.");
            }

            var directory = Path.GetFullPath(Path.Combine(currentAssemblyDirectory, $"../../../AITests/prompts"));

            PromptManager manager = new(new()
            {
                PromptFolder = directory
            });

            var exception = Assert.Throws<ArgumentException>(() => manager.GetPrompt("does_not_exist"));
            Assert.StartsWith($"Directory doesn't exist", exception.Message);
        }
    }
}
