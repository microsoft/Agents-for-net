﻿namespace Microsoft.Agents.Extensions.Teams.AI.Planners
{
    internal sealed class ParsedCommandResult
    {
        public int Length { get; set; }
        public IPredictedCommand Command { get; set; }
        public ParsedCommandResult(int length, IPredictedCommand command)
        {
            Length = length;
            Command = command;
        }
    }
}
