using System;
using Microsoft.Agents.Core.Models;
using System.Text.Json;

public sealed class ActivityJsonFormatterTests
{
    [Fact]
    public void Format_UsesProtocolNamesAndIndentation()
    {
        Activity activity = new()
        {
            Type = ActivityTypes.Message,
            Text = "hello",
            Entities = [new Entity("custom") { Properties = { ["answer"] = JsonSerializer.SerializeToElement(42) } }]
        };

        string json = ActivityJsonFormatter.Format(activity);

        Assert.Contains(Environment.NewLine, json);
        Assert.Contains("\"type\": \"message\"", json);
        Assert.Contains("\"answer\": 42", json);
    }
}
