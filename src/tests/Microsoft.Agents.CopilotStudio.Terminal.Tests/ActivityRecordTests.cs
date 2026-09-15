using System;
using Microsoft.Agents.Core.Models;

public sealed class ActivityRecordTests
{
    [Fact]
    public void ToString_FormatsListSummaryWithoutActivityOrJsonPayload()
    {
        ActivityRecord record = new(
            42,
            ActivityDirection.Inbound,
            DateTimeOffset.Parse("2026-09-11T12:00:00Z"),
            ActivityTypes.Message,
            "Hello",
            """{"text":"secret JSON body"}""",
            null);

        string display = record.ToString();

        Assert.Contains("42", display);
        Assert.Contains("Inbound", display);
        Assert.Contains("message", display, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Hello", display);
        Assert.DoesNotContain("secret JSON body", display);
    }
}
