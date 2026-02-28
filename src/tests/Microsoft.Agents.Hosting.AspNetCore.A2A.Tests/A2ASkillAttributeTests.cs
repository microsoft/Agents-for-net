// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Agents.Hosting.AspNetCore.A2A.Tests
{
    public class A2ASkillAttributeTests
    {
        [Fact]
        public void Constructor_WithNameAndTags_InitializesProperties()
        {
            // Arrange
            var name = "TestSkill";
            var tags = "tag1,tag2,tag3";

            // Act
            var attribute = new A2ASkillAttribute(name, tags);

            // Assert
            Assert.NotNull(attribute);
            Assert.Equal(name, attribute.Name);
            Assert.Equal(name, attribute.Id); // Id defaults to Name when not provided
            Assert.Equal(name, attribute.Description); // Description defaults to Name when not provided
            Assert.NotNull(attribute.Tags);
            Assert.Equal(3, attribute.Tags.Count);
            Assert.Contains("tag1", attribute.Tags);
            Assert.Contains("tag2", attribute.Tags);
            Assert.Contains("tag3", attribute.Tags);
        }

        [Fact]
        public void Constructor_WithAllParameters_InitializesAllProperties()
        {
            // Arrange
            var name = "TestSkill";
            var tags = "tag1,tag2";
            var id = "custom-id";
            var description = "Custom description";
            var examples = "example1;example2";
            var inputModes = "text,audio";
            var outputModes = "text,video";

            // Act
            var attribute = new A2ASkillAttribute(name, tags, id, description, examples, inputModes, outputModes);

            // Assert
            Assert.Equal(name, attribute.Name);
            Assert.Equal(id, attribute.Id);
            Assert.Equal(description, attribute.Description);
            Assert.Equal(2, attribute.Tags.Count);
            Assert.Contains("tag1", attribute.Tags);
            Assert.Contains("tag2", attribute.Tags);
            Assert.NotNull(attribute.Examples);
            Assert.Equal(2, attribute.Examples.Count);
            Assert.Contains("example1", attribute.Examples);
            Assert.Contains("example2", attribute.Examples);
            Assert.NotNull(attribute.InputModes);
            Assert.Equal(2, attribute.InputModes.Count);
            Assert.Contains("text", attribute.InputModes);
            Assert.Contains("audio", attribute.InputModes);
            Assert.NotNull(attribute.OutputModes);
            Assert.Equal(2, attribute.OutputModes.Count);
            Assert.Contains("text", attribute.OutputModes);
            Assert.Contains("video", attribute.OutputModes);
        }

        [Theory]
        [InlineData("tag1,tag2,tag3")]
        [InlineData("tag1 tag2 tag3")]
        [InlineData("tag1;tag2;tag3")]
        [InlineData("tag1, tag2; tag3")]
        public void Constructor_ParsesTagsWithMultipleDelimiters(string tags)
        {
            // Arrange
            var name = "TestSkill";

            // Act
            var attribute = new A2ASkillAttribute(name, tags);

            // Assert
            Assert.NotNull(attribute.Tags);
            Assert.Equal(3, attribute.Tags.Count);
            Assert.Contains("tag1", attribute.Tags);
            Assert.Contains("tag2", attribute.Tags);
            Assert.Contains("tag3", attribute.Tags);
        }

        [Theory]
        [InlineData("example1;example2;example3")]
        [InlineData("example1; example2 ; example3")]
        public void Constructor_ParsesExamplesWithSemicolonDelimiter(string examples)
        {
            // Arrange
            var name = "TestSkill";
            var tags = "tag1";

            // Act
            var attribute = new A2ASkillAttribute(name, tags, examples: examples);

            // Assert
            Assert.NotNull(attribute.Examples);
            Assert.Equal(3, attribute.Examples.Count);
            Assert.Contains("example1", attribute.Examples);
            Assert.Contains("example2", attribute.Examples);
            Assert.Contains("example3", attribute.Examples);
        }

        [Theory]
        [InlineData("mode1,mode2,mode3")]
        [InlineData("mode1 mode2 mode3")]
        [InlineData("mode1;mode2;mode3")]
        [InlineData("mode1, mode2; mode3")]
        public void Constructor_ParsesInputModesWithMultipleDelimiters(string inputModes)
        {
            // Arrange
            var name = "TestSkill";
            var tags = "tag1";

            // Act
            var attribute = new A2ASkillAttribute(name, tags, inputModes: inputModes);

            // Assert
            Assert.NotNull(attribute.InputModes);
            Assert.Equal(3, attribute.InputModes.Count);
            Assert.Contains("mode1", attribute.InputModes);
            Assert.Contains("mode2", attribute.InputModes);
            Assert.Contains("mode3", attribute.InputModes);
        }

        [Theory]
        [InlineData("mode1,mode2,mode3")]
        [InlineData("mode1 mode2 mode3")]
        [InlineData("mode1;mode2;mode3")]
        [InlineData("mode1, mode2; mode3")]
        public void Constructor_ParsesOutputModesWithMultipleDelimiters(string outputModes)
        {
            // Arrange
            var name = "TestSkill";
            var tags = "tag1";

            // Act
            var attribute = new A2ASkillAttribute(name, tags, outputModes: outputModes);

            // Assert
            Assert.NotNull(attribute.OutputModes);
            Assert.Equal(3, attribute.OutputModes.Count);
            Assert.Contains("mode1", attribute.OutputModes);
            Assert.Contains("mode2", attribute.OutputModes);
            Assert.Contains("mode3", attribute.OutputModes);
        }

        [Fact]
        public void Constructor_WithEmptyExamples_SetsExamplesToNull()
        {
            // Arrange
            var name = "TestSkill";
            var tags = "tag1";

            // Act
            var attribute = new A2ASkillAttribute(name, tags, examples: "");

            // Assert
            Assert.Null(attribute.Examples);
        }

        [Fact]
        public void Constructor_WithNullExamples_SetsExamplesToNull()
        {
            // Arrange
            var name = "TestSkill";
            var tags = "tag1";

            // Act
            var attribute = new A2ASkillAttribute(name, tags, examples: null);

            // Assert
            Assert.Null(attribute.Examples);
        }

        [Fact]
        public void Constructor_WithEmptyInputModes_SetsInputModesToNull()
        {
            // Arrange
            var name = "TestSkill";
            var tags = "tag1";

            // Act
            var attribute = new A2ASkillAttribute(name, tags, inputModes: "");

            // Assert
            Assert.Null(attribute.InputModes);
        }

        [Fact]
        public void Constructor_WithNullInputModes_SetsInputModesToNull()
        {
            // Arrange
            var name = "TestSkill";
            var tags = "tag1";

            // Act
            var attribute = new A2ASkillAttribute(name, tags, inputModes: null);

            // Assert
            Assert.Null(attribute.InputModes);
        }

        [Fact]
        public void Constructor_WithEmptyOutputModes_SetsOutputModesToNull()
        {
            // Arrange
            var name = "TestSkill";
            var tags = "tag1";

            // Act
            var attribute = new A2ASkillAttribute(name, tags, outputModes: "");

            // Assert
            Assert.Null(attribute.OutputModes);
        }

        [Fact]
        public void Constructor_WithNullOutputModes_SetsOutputModesToNull()
        {
            // Arrange
            var name = "TestSkill";
            var tags = "tag1";

            // Act
            var attribute = new A2ASkillAttribute(name, tags, outputModes: null);

            // Assert
            Assert.Null(attribute.OutputModes);
        }

        [Fact]
        public void Constructor_WithNullNameAndId_ThrowsArgumentNullException()
        {
            // Arrange
            var tags = "tag1";

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new A2ASkillAttribute(null, tags, id: null));
        }

        [Fact]
        public void Constructor_WithEmptyNameAndId_ThrowsArgumentException()
        {
            // Arrange
            var tags = "tag1";

            // Act & Assert
            Assert.Throws<ArgumentException>(() => new A2ASkillAttribute("", tags, id: ""));
        }

        [Fact]
        public void Constructor_WithNullTags_ThrowsArgumentNullException()
        {
            // Arrange
            var name = "TestSkill";

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new A2ASkillAttribute(name, null));
        }

        [Fact]
        public void Constructor_WithEmptyTags_ThrowsArgumentException()
        {
            // Arrange
            var name = "TestSkill";

            // Act & Assert
            Assert.Throws<ArgumentException>(() => new A2ASkillAttribute(name, ""));
        }

        [Fact]
        public void Constructor_WithOnlyIdProvided_UsesIdForName()
        {
            // Arrange
            var tags = "tag1";
            var id = "custom-id";

            // Act
            var attribute = new A2ASkillAttribute(null, tags, id);

            // Assert
            Assert.Equal(id, attribute.Id);
            Assert.Null(attribute.Name);
        }

        [Fact]
        public void Constructor_RemovesEmptyEntriesFromTags()
        {
            // Arrange
            var name = "TestSkill";
            var tags = "tag1,,tag2,  ,tag3";

            // Act
            var attribute = new A2ASkillAttribute(name, tags);

            // Assert
            Assert.Equal(3, attribute.Tags.Count);
            Assert.Contains("tag1", attribute.Tags);
            Assert.Contains("tag2", attribute.Tags);
            Assert.Contains("tag3", attribute.Tags);
        }

        [Fact]
        public void Constructor_RemovesEmptyEntriesFromExamples()
        {
            // Arrange
            var name = "TestSkill";
            var tags = "tag1";
            var examples = "example1;;example2;  ;example3";

            // Act
            var attribute = new A2ASkillAttribute(name, tags, examples: examples);

            // Assert
            Assert.Equal(3, attribute.Examples.Count);
            Assert.Contains("example1", attribute.Examples);
            Assert.Contains("example2", attribute.Examples);
            Assert.Contains("example3", attribute.Examples);
        }

        [Fact]
        public void Constructor_RemovesEmptyEntriesFromInputModes()
        {
            // Arrange
            var name = "TestSkill";
            var tags = "tag1";
            var inputModes = "mode1,,mode2,  ,mode3";

            // Act
            var attribute = new A2ASkillAttribute(name, tags, inputModes: inputModes);

            // Assert
            Assert.Equal(3, attribute.InputModes.Count);
            Assert.Contains("mode1", attribute.InputModes);
            Assert.Contains("mode2", attribute.InputModes);
            Assert.Contains("mode3", attribute.InputModes);
        }

        [Fact]
        public void Constructor_RemovesEmptyEntriesFromOutputModes()
        {
            // Arrange
            var name = "TestSkill";
            var tags = "tag1";
            var outputModes = "mode1,,mode2,  ,mode3";

            // Act
            var attribute = new A2ASkillAttribute(name, tags, outputModes: outputModes);

            // Assert
            Assert.Equal(3, attribute.OutputModes.Count);
            Assert.Contains("mode1", attribute.OutputModes);
            Assert.Contains("mode2", attribute.OutputModes);
            Assert.Contains("mode3", attribute.OutputModes);
        }

        [Fact]
        public void AttributeUsage_IsCorrectlyConfigured()
        {
            // Act
            var attributeUsage = typeof(A2ASkillAttribute)
                .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
                .FirstOrDefault() as AttributeUsageAttribute;

            // Assert
            Assert.NotNull(attributeUsage);
            Assert.Equal(AttributeTargets.Class, attributeUsage.ValidOn);
            Assert.True(attributeUsage.Inherited);
            Assert.True(attributeUsage.AllowMultiple);
        }

        [Fact]
        public void ExamplesProperty_CanBeSetAndRetrieved()
        {
            // Arrange
            var name = "TestSkill";
            var tags = "tag1";
            var attribute = new A2ASkillAttribute(name, tags);
            var newExamples = new List<string> { "new1", "new2" };

            // Act
            attribute.Examples = newExamples;

            // Assert
            Assert.Equal(newExamples, attribute.Examples);
            Assert.Equal(2, attribute.Examples.Count);
        }

        [Fact]
        public void InputModesProperty_CanBeSetAndRetrieved()
        {
            // Arrange
            var name = "TestSkill";
            var tags = "tag1";
            var attribute = new A2ASkillAttribute(name, tags);
            var newInputModes = new List<string> { "mode1", "mode2" };

            // Act
            attribute.InputModes = newInputModes;

            // Assert
            Assert.Equal(newInputModes, attribute.InputModes);
            Assert.Equal(2, attribute.InputModes.Count);
        }

        [Fact]
        public void OutputModesProperty_CanBeSetAndRetrieved()
        {
            // Arrange
            var name = "TestSkill";
            var tags = "tag1";
            var attribute = new A2ASkillAttribute(name, tags);
            var newOutputModes = new List<string> { "mode1", "mode2" };

            // Act
            attribute.OutputModes = newOutputModes;

            // Assert
            Assert.Equal(newOutputModes, attribute.OutputModes);
            Assert.Equal(2, attribute.OutputModes.Count);
        }

        [Fact]
        public void Constructor_WithWhitespaceInDelimitedStrings_TrimsValues()
        {
            // Arrange
            var name = "TestSkill";
            var tags = " tag1 , tag2 ; tag3 ";
            var examples = " example1 ; example2 ";
            var inputModes = " mode1 , mode2 ";
            var outputModes = " mode3 ; mode4 ";

            // Act
            var attribute = new A2ASkillAttribute(name, tags, examples: examples, inputModes: inputModes, outputModes: outputModes);

            // Assert
            Assert.All(attribute.Tags, tag => Assert.DoesNotContain(" ", tag));
            Assert.All(attribute.Examples, example => Assert.DoesNotContain(" ", example.Trim()));
            Assert.All(attribute.InputModes, mode => Assert.DoesNotContain(" ", mode));
            Assert.All(attribute.OutputModes, mode => Assert.DoesNotContain(" ", mode));
        }
    }
}