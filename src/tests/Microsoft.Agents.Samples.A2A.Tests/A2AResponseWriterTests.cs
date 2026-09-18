// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Samples.A2AClient;
using Xunit;

namespace Microsoft.Agents.Samples.A2A.Tests;

public class A2AResponseWriterTests
{
    [Fact]
    public void Format_TaskWithStatusAndArtifact_ReturnsOnlyTextContent()
    {
        const string sentinelToken = "sentinel-access-token";
        var task = new AgentTask
        {
            Id = "task-1",
            ContextId = "context-1",
            Status = new TaskStatus
            {
                State = TaskState.Completed,
                Message = new Message
                {
                    MessageId = "message-1",
                    Role = Role.Agent,
                    Parts = [Part.FromText("status text")],
                },
            },
            Artifacts =
            [
                new Artifact
                {
                    ArtifactId = "artifact-1",
                    Name = $"request headers: Authorization: Bearer {sentinelToken}",
                    Parts = [Part.FromText("artifact text")],
                },
            ],
        };

        string output = A2AResponseWriter.Format(task);

        Assert.Contains("status text", output);
        Assert.Contains("artifact text", output);
        Assert.DoesNotContain("request headers", output);
        Assert.DoesNotContain(sentinelToken, output);
    }
}
