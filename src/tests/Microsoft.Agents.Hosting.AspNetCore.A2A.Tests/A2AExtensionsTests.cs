// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using System.Collections.Generic;

#nullable enable

namespace Microsoft.Agents.Hosting.AspNetCore.A2A.Tests;

public class A2AExtensionsTests
{
    #region IsTerminal Tests

    [Fact]
    public void IsTerminal_CompletedState_ReturnsTrue()
    {
        // Arrange
        var task = new AgentTask()
        {
            Id = "task123",
            Status = new AgentTaskStatus()
            {
                State = TaskState.Completed
            }
        };

        // Act
        var result = task.IsTerminal();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsTerminal_CanceledState_ReturnsTrue()
    {
        // Arrange
        var task = new AgentTask()
        {
            Id = "task123",
            Status = new AgentTaskStatus()
            {
                State = TaskState.Canceled
            }
        };

        // Act
        var result = task.IsTerminal();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsTerminal_RejectedState_ReturnsTrue()
    {
        // Arrange
        var task = new AgentTask()
        {
            Id = "task123",
            Status = new AgentTaskStatus()
            {
                State = TaskState.Rejected
            }
        };

        // Act
        var result = task.IsTerminal();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsTerminal_FailedState_ReturnsTrue()
    {
        // Arrange
        var task = new AgentTask()
        {
            Id = "task123",
            Status = new AgentTaskStatus()
            {
                State = TaskState.Failed
            }
        };

        // Act
        var result = task.IsTerminal();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsTerminal_SubmittedState_ReturnsFalse()
    {
        // Arrange
        var task = new AgentTask()
        {
            Id = "task123",
            Status = new AgentTaskStatus()
            {
                State = TaskState.Submitted
            }
        };

        // Act
        var result = task.IsTerminal();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsTerminal_WorkingState_ReturnsFalse()
    {
        // Arrange
        var task = new AgentTask()
        {
            Id = "task123",
            Status = new AgentTaskStatus()
            {
                State = TaskState.Working
            }
        };

        // Act
        var result = task.IsTerminal();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsTerminal_InputRequiredState_ReturnsFalse()
    {
        // Arrange
        var task = new AgentTask()
        {
            Id = "task123",
            Status = new AgentTaskStatus()
            {
                State = TaskState.InputRequired
            }
        };

        // Act
        var result = task.IsTerminal();

        // Assert
        Assert.False(result);
    }

    #endregion

    #region ToA2AMetadata Tests

    [Fact]
    public void ToA2AMetadata_SimpleObject_ReturnsValidMetadata()
    {
        // Arrange
        var testData = new TestDataClass
        {
            Name = "TestName",
            Value = 42
        };
        var contentType = "application/json";

        // Act
        var result = testData.ToA2AMetadata(contentType);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("mimeType"));
        Assert.Equal(contentType, result["mimeType"].GetString());
        Assert.True(result.ContainsKey("type"));
        Assert.Equal("object", result["type"].GetString());
        Assert.True(result.ContainsKey("schema"));
    }

    [Fact]
    public void ToA2AMetadata_DifferentContentType_UsesProvidedContentType()
    {
        // Arrange
        var testData = new TestDataClass
        {
            Name = "TestName",
            Value = 42
        };
        var contentType = "application/vnd.custom+json";

        // Act
        var result = testData.ToA2AMetadata(contentType);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(contentType, result["mimeType"].GetString());
    }

    [Fact]
    public void ToA2AMetadata_SameObjectTwice_UsesCachedSchema()
    {
        // Arrange
        var testData1 = new TestDataClass
        {
            Name = "TestName1",
            Value = 1
        };
        var testData2 = new TestDataClass
        {
            Name = "TestName2",
            Value = 2
        };
        var contentType = "application/json";

        // Act
        var result1 = testData1.ToA2AMetadata(contentType);
        var result2 = testData2.ToA2AMetadata(contentType);

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        // Both should have the same schema structure (cached)
        Assert.Equal(result1["schema"].ToString(), result2["schema"].ToString());
    }

    [Fact]
    public void ToA2AMetadata_ComplexObject_ReturnsValidMetadata()
    {
        // Arrange
        var complexData = new ComplexTestData
        {
            SimpleData = new TestDataClass { Name = "Nested", Value = 100 },
            Items = new List<string> { "item1", "item2" },
            Flag = true
        };
        var contentType = "application/json";

        // Act
        var result = complexData.ToA2AMetadata(contentType);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("mimeType"));
        Assert.True(result.ContainsKey("type"));
        Assert.True(result.ContainsKey("schema"));
        var schemaElement = result["schema"];
        Assert.NotEqual(default, schemaElement);
    }

    [Fact]
    public void ToA2AMetadata_ObjectWithNullableProperties_ReturnsValidMetadata()
    {
        // Arrange
        var testData = new NullableTestData
        {
            RequiredValue = "Required",
            OptionalValue = null
        };
        var contentType = "application/json";

        // Act
        var result = testData.ToA2AMetadata(contentType);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("schema"));
    }

    [Fact]
    public void ToA2AMetadata_DifferentObjectTypes_GeneratesDifferentSchemas()
    {
        // Arrange
        var testData1 = new TestDataClass { Name = "Test", Value = 1 };
        var testData2 = new DifferentTestData { Description = "Different", Count = 5 };
        var contentType = "application/json";

        // Act
        var result1 = testData1.ToA2AMetadata(contentType);
        var result2 = testData2.ToA2AMetadata(contentType);

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        // Schemas should be different for different types
        Assert.NotEqual(result1["schema"].ToString(), result2["schema"].ToString());
    }

    #endregion

    #region Test Helper Classes

    private class TestDataClass
    {
        public required string Name { get; set; }
        public int Value { get; set; }
    }

    private class ComplexTestData
    {
        public required TestDataClass SimpleData { get; set; }
        public required List<string> Items { get; set; }
        public bool Flag { get; set; }
    }

    private class NullableTestData
    {
        public required string RequiredValue { get; set; }
        public string? OptionalValue { get; set; }
    }

    private class DifferentTestData
    {
        public required string Description { get; set; }
        public int Count { get; set; }
    }

    #endregion
}