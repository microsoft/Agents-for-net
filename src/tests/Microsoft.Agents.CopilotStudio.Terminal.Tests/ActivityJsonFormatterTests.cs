#nullable enable

using System;

public sealed class ActivityJsonFormatterTests
{
    [Fact]
    public void Format_IndentsExistingProtocolJsonWithoutReserializingAnActivity()
    {
        string json = ActivityJsonFormatter.Format(
            """{"type":"message","entities":[{"answer":42}]}""");

        Assert.Contains(Environment.NewLine, json);
        Assert.Contains("\"type\": \"message\"", json);
        Assert.Contains("\"answer\": 42", json);
    }
}
