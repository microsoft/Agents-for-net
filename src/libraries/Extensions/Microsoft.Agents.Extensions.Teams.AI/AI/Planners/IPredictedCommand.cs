﻿using Microsoft.Agents.Extensions.Teams.AI.Utilities.JsonConverters;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Extensions.Teams.AI.Planners
{
    /// <summary>
    /// A predicted command is a command that the AI system should execute.
    /// </summary>
    [JsonConverter(typeof(PredictedCommandJsonConverter))]
    public interface IPredictedCommand
    {
        /// <summary>
        /// The type of command to execute.
        /// </summary>
        string Type { get; }
    }
}
