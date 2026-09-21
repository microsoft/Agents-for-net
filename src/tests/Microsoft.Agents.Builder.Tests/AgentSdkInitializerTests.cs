// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using System;
using System.Reflection;
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
        private static class InvalidSdkInitializer
        {
            public static void Init(string value)
            {
            }
        }

        [Fact]
        public void EnsureInitialized_InvokesAssemblyInitializerOnce()
        {
            AgentSdkInitializer.EnsureInitialized();
            AgentSdkInitializer.EnsureInitialized();

            Assert.Equal(1, TestSdkInitializer.InitializationCount);
        }

        [Fact]
        public void GetInitializationMethod_InvalidSignature_ThrowsClearError()
        {
            var getInitializationMethod = typeof(AgentSdkInitializer).GetMethod(
                "GetInitializationMethod",
                BindingFlags.NonPublic | BindingFlags.Static);
            var exception = Assert.Throws<TargetInvocationException>(
                () => getInitializationMethod.Invoke(null, new object[] { typeof(InvalidSdkInitializer) }));
            var configurationException = Assert.IsType<InvalidOperationException>(exception.InnerException);

            Assert.Contains("public static void Init()", configurationException.Message);
        }
    }
}
