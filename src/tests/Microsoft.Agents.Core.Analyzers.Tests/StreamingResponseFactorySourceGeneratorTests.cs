// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Microsoft.Agents.Core.Analyzers.Tests
{
    public class StreamingResponseFactorySourceGeneratorTests
    {
        private static IEnumerable<MetadataReference> GetReferences()
        {
            var trusted = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
            if (trusted != null)
                foreach (var path in trusted.Split(Path.PathSeparator))
                    if (File.Exists(path))
                        yield return MetadataReference.CreateFromFile(path);

            // Builder assembly provides StreamingResponseFactoryAttribute and IStreamingResponseFactory
            yield return MetadataReference.CreateFromFile(
                typeof(Microsoft.Agents.Builder.IStreamingResponseFactory).Assembly.Location);
        }

        private static CSharpGeneratorDriver RunGenerator(string source)
        {
            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                new[] { CSharpSyntaxTree.ParseText(source) },
                GetReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var generator = new StreamingResponseFactorySourceGenerator();
            var driver = CSharpGeneratorDriver.Create(generator);
            return (CSharpGeneratorDriver)driver.RunGenerators(compilation);
        }

        [Fact]
        public void NoDecoratedClasses_ProducesNoOutput()
        {
            var source = """
                namespace MyApp
                {
                    public class NotAFactory { }
                }
                """;

            var result = RunGenerator(source).GetRunResult();
            Assert.Empty(result.Results.Single().GeneratedSources);
        }

        [Fact]
        public void OneFactory_GeneratesOneAssemblyAttribute()
        {
            var source = """
                using Microsoft.Agents.Builder;

                namespace MyExtension
                {
                    [StreamingResponseFactory("slack")]
                    public class SlackStreamingFactory : IStreamingResponseFactory
                    {
                        public IStreamingResponse Create(ITurnContext ctx) => null;
                    }
                }
                """;

            var result = RunGenerator(source).GetRunResult();
            var generated = Assert.Single(result.Results.Single().GeneratedSources);
            var text = generated.SourceText.ToString();

            Assert.Contains("StreamingResponseFactoryAssemblyAttribute", text);
            Assert.Contains("global::MyExtension.SlackStreamingFactory", text);
            Assert.Contains("\"slack\"", text);
        }

        [Fact]
        public void MultipleChannels_OnSameClass_GeneratesMultipleAttributes()
        {
            var source = """
                using Microsoft.Agents.Builder;

                namespace MyExtension
                {
                    [StreamingResponseFactory("slack")]
                    [StreamingResponseFactory("discord")]
                    public class MultiChannelFactory : IStreamingResponseFactory
                    {
                        public IStreamingResponse Create(ITurnContext ctx) => null;
                    }
                }
                """;

            var result = RunGenerator(source).GetRunResult();
            var text = Assert.Single(result.Results.Single().GeneratedSources).SourceText.ToString();

            Assert.Contains("\"slack\"", text);
            Assert.Contains("\"discord\"", text);
        }

        [Fact]
        public void TwoFactories_GeneratesTwoAttributes()
        {
            var source = """
                using Microsoft.Agents.Builder;

                namespace MyExtension
                {
                    [StreamingResponseFactory("slack")]
                    public class SlackFactory : IStreamingResponseFactory
                    {
                        public IStreamingResponse Create(ITurnContext ctx) => null;
                    }

                    [StreamingResponseFactory("discord")]
                    public class DiscordFactory : IStreamingResponseFactory
                    {
                        public IStreamingResponse Create(ITurnContext ctx) => null;
                    }
                }
                """;

            var result = RunGenerator(source).GetRunResult();
            var text = Assert.Single(result.Results.Single().GeneratedSources).SourceText.ToString();

            Assert.Contains("global::MyExtension.SlackFactory", text);
            Assert.Contains("global::MyExtension.DiscordFactory", text);
        }

        [Fact]
        public void EscapedChannelId_IsEmittedAsValidStringLiteral()
        {
            var source = """
                using Microsoft.Agents.Builder;

                namespace MyExtension
                {
                    [StreamingResponseFactory("say \"hello\"")]
                    public class EscapedFactory : IStreamingResponseFactory
                    {
                        public IStreamingResponse Create(ITurnContext ctx) => null;
                    }
                }
                """;

            var generated = Assert.Single(RunGenerator(source).GetRunResult()
                .Results.Single().GeneratedSources);
            var text = generated.SourceText.ToString();

            Assert.Contains("\"say \\\"hello\\\"\"", text);
        }

        [Fact]
        public void GeneratedFile_HasExpectedHintName()
        {
            var source = """
                using Microsoft.Agents.Builder;

                namespace MyExtension
                {
                    [StreamingResponseFactory("slack")]
                    public class SlackFactory : IStreamingResponseFactory
                    {
                        public IStreamingResponse Create(ITurnContext ctx) => null;
                    }
                }
                """;

            var generated = RunGenerator(source).GetRunResult()
                .Results.Single().GeneratedSources.Single();

            Assert.Equal("StreamingResponseFactoryAssemblyAttributes.g.cs", generated.HintName);
        }

        [Fact]
        public void Generator_ProducesNoDiagnostics()
        {
            var source = """
                using Microsoft.Agents.Builder;

                namespace MyExtension
                {
                    [StreamingResponseFactory("slack")]
                    public class SlackFactory : IStreamingResponseFactory
                    {
                        public IStreamingResponse Create(ITurnContext ctx) => null;
                    }
                }
                """;

            var result = RunGenerator(source).GetRunResult();
            Assert.Empty(result.Diagnostics);
        }
    }
}
