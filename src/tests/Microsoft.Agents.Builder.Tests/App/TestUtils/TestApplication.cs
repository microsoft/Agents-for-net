﻿

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Storage;
using System;

namespace Microsoft.Agents.Builder.Tests.App.TestUtils
{
    public class TestApplication : AgentApplication
    {
        public TestApplication(TestApplicationOptions options) : base(options)
        {
            ArgumentNullException.ThrowIfNull(options);

            options.StartTypingTimer = false;
        }
    }

    public class TestApplicationOptions(IStorage storage) : AgentApplicationOptions(storage) { }
}
