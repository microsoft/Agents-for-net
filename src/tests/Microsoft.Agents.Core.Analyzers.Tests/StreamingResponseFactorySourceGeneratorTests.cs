// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.Agents.Core.Analyzers.Tests
{
    public class StreamingResponseFactorySourceGeneratorTests
    {
        [Fact]
        public void RecordFactory_GeneratesAssemblyAttribute()
        {
            var source = """
                using System;

                namespace Microsoft.Agents.Builder
                {
                    [AttributeUsage(AttributeTargets.Class)]
                    public sealed class StreamingResponseFactoryAttribute(string channelId) : Attribute;

                    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
                    public sealed class StreamingResponseFactoryAssemblyAttribute(Type type) : Attribute;
                }

                namespace TestApp
                {
                    [Microsoft.Agents.Builder.StreamingResponseFactory("test")]
                    public record TestStreamingResponseFactory;
                }
                """;

            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                [CSharpSyntaxTree.ParseText(source)],
                GetReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var driver = CSharpGeneratorDriver.Create(new StreamingResponseFactorySourceGenerator());
            var result = driver.RunGenerators(compilation).GetRunResult();
            var generated = Assert.Single(result.Results.Single().GeneratedSources).SourceText.ToString();

            Assert.Contains(
                "[assembly: Microsoft.Agents.Builder.StreamingResponseFactoryAssemblyAttribute(typeof(global::TestApp.TestStreamingResponseFactory))]",
                generated);
        }

        private static IEnumerable<MetadataReference> GetReferences()
        {
            var trusted = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
            if (trusted != null)
            {
                foreach (var path in trusted.Split(Path.PathSeparator))
                {
                    if (File.Exists(path))
                    {
                        yield return MetadataReference.CreateFromFile(path);
                    }
                }
            }
        }
    }
}
