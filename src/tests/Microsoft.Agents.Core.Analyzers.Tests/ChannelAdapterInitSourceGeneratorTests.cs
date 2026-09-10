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
    public class ChannelAdapterInitSourceGeneratorTests
    {
        /// <summary>
        /// A minimal inline declaration of the <c>ChannelAdapterAttribute</c> under the exact metadata name
        /// the generator matches, so the test does not need a reference to the Hosting.AspNetCore assembly.
        /// </summary>
        private const string AttributeDeclaration = """
            namespace Microsoft.Agents.Builder.Adapters
            {
                [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true)]
                public sealed class ChannelAdapterAttribute : System.Attribute
                {
                    public ChannelAdapterAttribute(string channelId) { ChannelId = channelId; }
                    public string ChannelId { get; }
                }
            }
            """;

        private static IEnumerable<MetadataReference> GetReferences()
        {
            var trusted = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
            if (trusted != null)
                foreach (var path in trusted.Split(Path.PathSeparator))
                    if (File.Exists(path))
                        yield return MetadataReference.CreateFromFile(path);
        }

        private static CSharpGeneratorDriver RunGenerator(string source)
        {
            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                new[]
                {
                    CSharpSyntaxTree.ParseText(AttributeDeclaration),
                    CSharpSyntaxTree.ParseText(source),
                },
                GetReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var generator = new ChannelAdapterInitSourceGenerator();
            var driver = CSharpGeneratorDriver.Create(generator);
            return (CSharpGeneratorDriver)driver.RunGenerators(compilation);
        }

        [Fact]
        public void NoChannelAdapterAttribute_ProducesNoOutput()
        {
            var source = """
                namespace MyApp
                {
                    public class NotAnnotated { }
                }
                """;

            var result = RunGenerator(source).GetRunResult();

            Assert.Empty(result.Results.Single().GeneratedSources);
        }

        [Fact]
        public void OneAnnotatedClass_GeneratesOneAssemblyAttribute()
        {
            var source = """
                namespace MyApp
                {
                    [Microsoft.Agents.Builder.Adapters.ChannelAdapter("msteams")]
                    public class TeamsChannelAdapter { }
                }
                """;

            var text = Assert.Single(RunGenerator(source).GetRunResult().Results.Single().GeneratedSources)
                .SourceText.ToString();

            Assert.Contains(
                "[assembly: Microsoft.Agents.Builder.Adapters.ChannelAdapterInitAssemblyAttribute(typeof(global::MyApp.TeamsChannelAdapter), \"msteams\")]",
                text);
        }

        [Fact]
        public void MultipleAnnotatedClasses_GenerateAllAttributes()
        {
            var source = """
                namespace MyApp
                {
                    [Microsoft.Agents.Builder.Adapters.ChannelAdapter("a")]
                    public class AdapterA { }

                    [Microsoft.Agents.Builder.Adapters.ChannelAdapter("b")]
                    public class AdapterB { }
                }
                """;

            var text = Assert.Single(RunGenerator(source).GetRunResult().Results.Single().GeneratedSources)
                .SourceText.ToString();

            Assert.Contains("typeof(global::MyApp.AdapterA), \"a\"", text);
            Assert.Contains("typeof(global::MyApp.AdapterB), \"b\"", text);
        }

        [Fact]
        public void ClassWithMultipleAttributes_EmitsOneAttributePerChannel()
        {
            var source = """
                namespace MyApp
                {
                    [Microsoft.Agents.Builder.Adapters.ChannelAdapter("slack")]
                    [Microsoft.Agents.Builder.Adapters.ChannelAdapter("webchat")]
                    public class MultiAdapter { }
                }
                """;

            var text = Assert.Single(RunGenerator(source).GetRunResult().Results.Single().GeneratedSources)
                .SourceText.ToString();

            Assert.Contains("typeof(global::MyApp.MultiAdapter), \"slack\"", text);
            Assert.Contains("typeof(global::MyApp.MultiAdapter), \"webchat\"", text);
        }

        [Fact]
        public void GeneratedFile_HasExpectedHintName()
        {
            var source = """
                namespace MyApp
                {
                    [Microsoft.Agents.Builder.Adapters.ChannelAdapter("a2a")]
                    public class MyAdapter { }
                }
                """;

            var generated = Assert.Single(RunGenerator(source).GetRunResult().Results.Single().GeneratedSources);

            Assert.Equal("ChannelAdapterInitAssemblyAttributes.g.cs", generated.HintName);
        }

        [Fact]
        public void Generator_ProducesNoDiagnostics()
        {
            var source = """
                namespace MyApp
                {
                    [Microsoft.Agents.Builder.Adapters.ChannelAdapter("a2a")]
                    public class MyAdapter { }
                }
                """;

            Assert.Empty(RunGenerator(source).GetRunResult().Diagnostics);
        }
    }
}
