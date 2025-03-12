﻿using Microsoft.Agents.Core.Models;
using System.Text.Json;

namespace Microsoft.Agents.Mcp.Server.Sample.Operations;

internal class ActivityData
{
    public string Type { get; init; } = "activity";

    public required Activity Content { get; init; }
}