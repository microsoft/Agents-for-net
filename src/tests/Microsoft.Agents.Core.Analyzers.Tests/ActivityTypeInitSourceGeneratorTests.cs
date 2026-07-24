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
    public class ActivityTypeInitSourceGeneratorTests
    {
        /// <summary>
        /// BCL platform references plus Microsoft.Agents.Core so tests can compile source that
        /// references <c>Microsoft.Agents.Core.Serialization.ActivityTypeAttribute</c>.
        /// </summary>
        private static IEnumerable<MetadataReference> GetReferences()
        {
            var trusted = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
            if (trusted != null)
                foreach (var path in trusted.Split(Path.PathSeparator))
                    if (File.Exists(path))
                        yield return MetadataReference.CreateFromFile(path);

            yield return MetadataReference.CreateFromFile(
                typeof(Microsoft.Agents.Core.Serialization.ActivityTypeAttribute).Assembly.Location);
        }

        private static CSharpGeneratorDriver RunGenerator(string source)
        {
            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                new[] { CSharpSyntaxTree.ParseText(source) },
                GetReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var generator = new ActivityTypeInitSourceGenerator();
            var driver = CSharpGeneratorDriver.Create(generator);
            return (CSharpGeneratorDriver)driver.RunGenerators(compilation);
        }

        [Fact]
        public void NoActivityTypeAttribute_ProducesNoOutput()
        {
            var source = """
                namespace MyApp
                {
                    public class NotAnnotated : Microsoft.Agents.Core.Models.Activity { }
                    public class AlsoNot { }
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
                    [Microsoft.Agents.Core.Serialization.ActivityType("x-workflowTrigger")]
                    public class WorkflowTriggerActivity : Microsoft.Agents.Core.Models.Activity { }
                }
                """;

            var text = Assert.Single(RunGenerator(source).GetRunResult().Results.Single().GeneratedSources)
                .SourceText.ToString();

            Assert.Contains(
                "[assembly: Microsoft.Agents.Core.Serialization.ActivityTypeInitAssemblyAttribute(typeof(global::MyApp.WorkflowTriggerActivity))]",
                text);
        }

        [Fact]
        public void ChannelIdOnlyAttribute_IsPickedUp()
        {
            var source = """
                namespace MyApp
                {
                    [Microsoft.Agents.Core.Serialization.ActivityType(ChannelId = "slack")]
                    public class SlackActivity : Microsoft.Agents.Core.Models.Activity { }
                }
                """;

            var text = Assert.Single(RunGenerator(source).GetRunResult().Results.Single().GeneratedSources)
                .SourceText.ToString();

            Assert.Contains("typeof(global::MyApp.SlackActivity)", text);
        }

        [Fact]
        public void MultipleAnnotatedClasses_GenerateAllAttributes()
        {
            var source = """
                namespace MyApp
                {
                    [Microsoft.Agents.Core.Serialization.ActivityType("a")]
                    public class ActivityA : Microsoft.Agents.Core.Models.Activity { }

                    [Microsoft.Agents.Core.Serialization.ActivityType("b")]
                    public class ActivityB : Microsoft.Agents.Core.Models.Activity { }
                }
                """;

            var text = Assert.Single(RunGenerator(source).GetRunResult().Results.Single().GeneratedSources)
                .SourceText.ToString();

            Assert.Contains("global::MyApp.ActivityA", text);
            Assert.Contains("global::MyApp.ActivityB", text);
        }

        [Fact]
        public void ClassWithMultipleAttributes_EmitsSingleTypeReference()
        {
            var source = """
                namespace MyApp
                {
                    [Microsoft.Agents.Core.Serialization.ActivityType("a")]
                    [Microsoft.Agents.Core.Serialization.ActivityType("b")]
                    public class MultiActivity : Microsoft.Agents.Core.Models.Activity { }
                }
                """;

            var text = Assert.Single(RunGenerator(source).GetRunResult().Results.Single().GeneratedSources)
                .SourceText.ToString();

            // The assembly attribute references the class once; the individual discriminators are read
            // off the [ActivityType] attributes at registration time.
            var occurrences = text.Split(new[] { "typeof(global::MyApp.MultiActivity)" }, StringSplitOptions.None).Length - 1;
            Assert.Equal(1, occurrences);
        }

        [Fact]
        public void UnannotatedClass_IsNotIncluded()
        {
            var source = """
                namespace MyApp
                {
                    public class Plain : Microsoft.Agents.Core.Models.Activity { }

                    [Microsoft.Agents.Core.Serialization.ActivityType("x-custom")]
                    public class Annotated : Microsoft.Agents.Core.Models.Activity { }
                }
                """;

            var text = Assert.Single(RunGenerator(source).GetRunResult().Results.Single().GeneratedSources)
                .SourceText.ToString();

            Assert.DoesNotContain("Plain", text);
            Assert.Contains("global::MyApp.Annotated", text);
        }

        [Fact]
        public void GeneratedFile_HasExpectedHintName()
        {
            var source = """
                namespace MyApp
                {
                    [Microsoft.Agents.Core.Serialization.ActivityType("x-custom")]
                    public class MyActivity : Microsoft.Agents.Core.Models.Activity { }
                }
                """;

            var generated = Assert.Single(RunGenerator(source).GetRunResult().Results.Single().GeneratedSources);

            Assert.Equal("ActivityTypeInitAssemblyAttributes.g.cs", generated.HintName);
        }

        [Fact]
        public void Generator_ProducesNoDiagnostics()
        {
            var source = """
                namespace MyApp
                {
                    [Microsoft.Agents.Core.Serialization.ActivityType("x-custom")]
                    public class MyActivity : Microsoft.Agents.Core.Models.Activity { }
                }
                """;

            Assert.Empty(RunGenerator(source).GetRunResult().Diagnostics);
        }
    }
}
