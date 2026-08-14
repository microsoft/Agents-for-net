// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using Xunit;

[assembly: AgentSdkInitAssemblyAttribute(typeof(Microsoft.Agents.Builder.Tests.TestSdkInitializer))]

namespace Microsoft.Agents.Builder.Tests
{
    public static class TestSdkInitializer
    {
        public static int InitializationCount { get; private set; }

        public static void Init()
        {
            InitializationCount++;
        }
    }

    public class AgentSdkInitializerTests
    {
        [Fact]
        public void EnsureInitialized_InvokesAssemblyInitializerOnce()
        {
            AgentSdkInitializer.EnsureInitialized();
            AgentSdkInitializer.EnsureInitialized();

            Assert.Equal(1, TestSdkInitializer.InitializationCount);
        }
    }
}
