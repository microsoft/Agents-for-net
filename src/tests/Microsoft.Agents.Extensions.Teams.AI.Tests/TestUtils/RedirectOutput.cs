﻿using Microsoft.Extensions.Logging;
using System.Text;
using Xunit.Abstractions;

namespace Microsoft.Agents.Extensions.Teams.AI.Tests.TestUtils
{
    public class RedirectOutput : TextWriter, ILogger
    {
        private readonly StringBuilder _log;
        private readonly ITestOutputHelper _output;

        public override Encoding Encoding => Encoding.UTF8;

        public RedirectOutput(ITestOutputHelper output)
        {
            _output = output;
            _log = new StringBuilder();
        }

        public override void WriteLine(string? message)
        {
            _output.WriteLine(message);
            _log.AppendLine(message);
        }

        public string GetLogs()
        {
            return _log.ToString();
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
#pragma warning disable CA1062 // Validate arguments of public methods
            var message = formatter(state, exception);
#pragma warning restore CA1062 // Validate arguments of public methods

            _output?.WriteLine(message);
            _log.AppendLine(message);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }


        IDisposable? ILogger.BeginScope<TState>(TState state)
        {
            return null;
        }
    }
}
