

using Microsoft.Agents.BotBuilder.App;
using System;

namespace Microsoft.Agents.BotBuilder.Tests.App.TestUtils
{
    public class TestApplication : AgentApplication
    {
        public TestApplication(TestApplicationOptions options) : base(options)
        {
            ArgumentNullException.ThrowIfNull(options);

            options.StartTypingTimer = false;
        }
    }

    public class TestApplicationOptions : AgentApplicationOptions { }
}
