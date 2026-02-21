// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Microsoft.Agents.Hosting.AspNetCore.A2A.Tests;

public class A2AActivityTests
{
    [Fact]
    public void ActivityFromMessageWithText()
    {
        // Arrange
        var message = new AgentMessage()
        {
            TaskId = "task123",
            ContextId = "context123",
            MessageId = "message123",
            Role = MessageRole.Agent,
            Parts = [new TextPart() { Text = "text" }]
        };

        var task = new AgentTask()
        {
            Id = "task123",
            ContextId = "context123",
            Status = new AgentTaskStatus() { State = TaskState.Submitted },
        };

        // Act
        var activity = A2AActivity.ActivityFromMessage("request123", task, message);

        // Assert
        Assert.NotNull(activity);
        Assert.NotNull(activity.Conversation);
        Assert.NotNull(activity.Recipient);
        Assert.NotNull(activity.From);
        Assert.NotNull(activity.ChannelData);

        Assert.Equal("request123", activity.RequestId);
        Assert.Equal("task123", activity.Conversation.Id);
        Assert.Equal("text", activity.Text);
        Assert.Equal(Channels.A2A, activity.ChannelId);
        Assert.Equal(message, activity.ChannelData);
        Assert.Equal(RoleTypes.Agent, activity.Recipient.Role);
        Assert.Equal(RoleTypes.User, activity.From.Role);
    }

    [Fact]
    public void ActivityFromMessageWithParts()
    {
        // Arrange
        var message = new AgentMessage()
        {
            TaskId = "task123",
            ContextId = "context123",
            MessageId = "message123",
            Role = MessageRole.Agent,
            Parts =
            [
                new TextPart() { Text = "part1" },
                new TextPart() { Text = "part2" },
                new DataPart() { Data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("{ \"key1\": \"value1\"}")},
                new FilePart() { File = new FileContent(new Uri("http://uri.com")) { Name = "file.txt" } }
            ],
        };

        var task = new AgentTask()
        {
            Id = "task123",
            ContextId = "context123",
            Status = new AgentTaskStatus() { State = TaskState.Submitted },
        };

        // Act
        var activity = A2AActivity.ActivityFromMessage("request123", task, message);

        // Assert
        Assert.NotNull(activity);
        Assert.Equal(2, activity.Attachments.Count);
        Assert.Equal("part1part2", activity.Text);

        var dataAttachment = activity.Attachments[0];
        Assert.NotNull(dataAttachment.Content);
        Assert.Equal("application/json", dataAttachment.ContentType);

        var fileAttachment = activity.Attachments[1];
        Assert.Equal("file.txt", fileAttachment.Name);
        Assert.Equal("http://uri.com/", fileAttachment.ContentUrl.ToString());
    }

    [Fact]
    public void ActivityFromMessageWithNullRequestId()
    {
        // Arrange
        var message = new AgentMessage()
        {
            TaskId = "task123",
            ContextId = "context123",
            MessageId = "message123",
            Role = MessageRole.Agent,
            Parts = [new TextPart() { Text = "text" }]
        };

        var task = new AgentTask()
        {
            Id = "task123",
            ContextId = "context123",
            Status = new AgentTaskStatus() { State = TaskState.Submitted },
        };

        // Act
        var activity = A2AActivity.ActivityFromMessage(null, task, message);

        // Assert
        Assert.NotNull(activity);
        Assert.NotEmpty(activity.RequestId);
    }

    [Fact]
    public void ActivityFromMessageWithNullTask()
    {
        // Arrange
        var message = new AgentMessage()
        {
            TaskId = "task123",
            ContextId = "context123",
            MessageId = "message123",
            Role = MessageRole.Agent,
            Parts = [new TextPart() { Text = "text" }]
        };

        // Act
        var activity = A2AActivity.ActivityFromMessage("request123", null, message);

        message.TaskId = null;
        var activity2 = A2AActivity.ActivityFromMessage("request123", null, message);

        // Assert
        Assert.NotNull(activity);
        Assert.NotNull(activity2);

        // Uses Message.TaskId since Task is null
        Assert.Equal("task123", activity.Conversation.Id);

        // Generates new TaskId since both Task and Message.TaskId are null
        Assert.NotEmpty(activity2.Conversation.Id);
    }

    [Fact]
    public void ActivityFromMessageWithNullMessage()
    {
        Assert.Throws<ArgumentNullException>(() => A2AActivity.ActivityFromMessage(null, null, null));
    }

    [Fact]
    public void ActivityHasContent()
    {
        var hasTextContent = new Activity()
        {
            Text = "text",
        };

        var hasAttachmentContent = new Activity()
        {
            Attachments = [new Attachment() { ContentType = "text/plain", ContentUrl = "http://uri.com/file.txt" }]
        };

        var noContent = new Activity();

        Assert.True(hasTextContent.HasA2AMessageContent());
        Assert.True(hasAttachmentContent.HasA2AMessageContent());
        Assert.False(noContent.HasA2AMessageContent());
    }

    [Fact]
    public void A2ATaskStateFromActivity()
    {
        var expectingInput = new Activity()
        {
            InputHint = InputHints.ExpectingInput
        };

        var acceptingInput = new Activity()
        {
            InputHint = InputHints.AcceptingInput
        };

        var noInputHint = new Activity()
        {
        };

        var notInputHint = new Activity()
        {
        };

        Assert.Equal(TaskState.InputRequired, expectingInput.GetA2ATaskState());
        Assert.Equal(TaskState.Working, noInputHint.GetA2ATaskState());
        Assert.Equal(TaskState.Working, acceptingInput.GetA2ATaskState());
        Assert.Equal(TaskState.Working, notInputHint.GetA2ATaskState());
    }

    [Fact]
    public void MessageFromActivity()
    {
        var activity = new Activity()
        {
            Text = "text",
            Attachments = [new Attachment() { ContentType = "text/plain", ContentUrl = "http://uri.com/file.txt" }],
            Value = new Dictionary<string, object>() { { "key", "value" } },
            Entities = [new ProductInfo() { Id = Channels.M365CopilotSubChannel }]
        };

        var message = A2AActivity.MessageFromActivity("context123", "task123", activity);

        Assert.NotNull(message);
        Assert.Equal("task123", message.TaskId);
        Assert.Equal("context123", message.ContextId);
        Assert.NotEmpty(message.MessageId);
        Assert.NotEmpty(message.Parts);
        Assert.Equal(4, message.Parts.Count);

        // Part 1: Text
        Assert.IsType<TextPart>(message.Parts[0], exactMatch: false);
        Assert.Equal("text", message.Parts[0].AsTextPart().Text);

        // Part 2: Value
        Assert.IsType<DataPart>(message.Parts[1], exactMatch: false);
        Assert.NotNull(message.Parts[1].AsDataPart().Data);
        Assert.True(message.Parts[1].AsDataPart().Data.TryGetValue("key", out var value));

        // Part 3: Attachments
        Assert.IsType<FilePart>(message.Parts[2], exactMatch: false);
        Assert.Equal("text/plain", message.Parts[2].AsFilePart().File.MimeType);
        Assert.Equal("http://uri.com/file.txt", message.Parts[2].AsFilePart().File.Uri.ToString());

        // Part 4: Entities
        Assert.IsType<DataPart>(message.Parts[3], exactMatch: false);
        var productInfo = message.Parts[3].AsDataPart().Data;
        Assert.NotNull(productInfo);
        Assert.True(productInfo.TryGetValue("id", out var idValue));
        Assert.Equal(Channels.M365CopilotSubChannel, idValue.ToString());
        Assert.True(productInfo.TryGetValue("type", out var typeValue));
        Assert.Equal(EntityTypes.ProductInfo, typeValue.ToString());
    }
}
