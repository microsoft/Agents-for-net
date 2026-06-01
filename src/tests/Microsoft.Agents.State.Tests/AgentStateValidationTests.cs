// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Storage;
using Xunit;

namespace Microsoft.Agents.State.Tests
{
    /// <summary>
    /// Tests for AgentState input validation.
    /// </summary>
    public class AgentStateValidationTests
    {
        [Fact]
        public async Task HasValue_WithNullName_ShouldThrow()
        {
            // Arrange
            var storage = new MemoryStorage();
            var userState = new UserState(storage);
            var context = TestUtilities.CreateEmptyContext();
            await userState.LoadAsync(context);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => userState.HasValue(null));
        }

        [Fact]
        public async Task HasValue_WithEmptyName_ShouldThrow()
        {
            // Arrange
            var storage = new MemoryStorage();
            var userState = new UserState(storage);
            var context = TestUtilities.CreateEmptyContext();
            await userState.LoadAsync(context);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => userState.HasValue(string.Empty));
        }

        [Fact]
        public async Task HasValue_WithWhitespaceName_ShouldThrow()
        {
            // Arrange
            var storage = new MemoryStorage();
            var userState = new UserState(storage);
            var context = TestUtilities.CreateEmptyContext();
            await userState.LoadAsync(context);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => userState.HasValue("   "));
        }

        [Fact]
        public async Task DeleteValue_WithNullName_ShouldThrow()
        {
            // Arrange
            var storage = new MemoryStorage();
            var userState = new UserState(storage);
            var context = TestUtilities.CreateEmptyContext();
            await userState.LoadAsync(context);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => userState.DeleteValue(null));
        }

        [Fact]
        public async Task DeleteValue_WithEmptyName_ShouldThrow()
        {
            // Arrange
            var storage = new MemoryStorage();
            var userState = new UserState(storage);
            var context = TestUtilities.CreateEmptyContext();
            await userState.LoadAsync(context);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => userState.DeleteValue(string.Empty));
        }

        [Fact]
        public async Task DeleteValue_WithWhitespaceName_ShouldThrow()
        {
            // Arrange
            var storage = new MemoryStorage();
            var userState = new UserState(storage);
            var context = TestUtilities.CreateEmptyContext();
            await userState.LoadAsync(context);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => userState.DeleteValue("   "));
        }

        [Fact]
        public async Task SetValue_WithNullName_ShouldThrow()
        {
            // Arrange
            var storage = new MemoryStorage();
            var userState = new UserState(storage);
            var context = TestUtilities.CreateEmptyContext();
            await userState.LoadAsync(context);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => userState.SetValue<string>(null, "value"));
        }

        [Fact]
        public async Task SetValue_WithEmptyName_ShouldThrow()
        {
            // Arrange
            var storage = new MemoryStorage();
            var userState = new UserState(storage);
            var context = TestUtilities.CreateEmptyContext();
            await userState.LoadAsync(context);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => userState.SetValue(string.Empty, "value"));
        }

        [Fact]
        public async Task SetValue_WithWhitespaceName_ShouldThrow()
        {
            // Arrange
            var storage = new MemoryStorage();
            var userState = new UserState(storage);
            var context = TestUtilities.CreateEmptyContext();
            await userState.LoadAsync(context);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => userState.SetValue("   ", "value"));
        }

        [Fact]
        public async Task GetValue_WithValidName_ShouldSucceed()
        {
            // Arrange
            var storage = new MemoryStorage();
            var userState = new UserState(storage);
            var context = TestUtilities.CreateEmptyContext();
            await userState.LoadAsync(context);
            userState.SetValue("validKey", "testValue");

            // Act
            var value = userState.GetValue<string>("validKey");

            // Assert
            Assert.Equal("testValue", value);
        }

        [Fact]
        public async Task HasValue_WithValidName_ShouldSucceed()
        {
            // Arrange
            var storage = new MemoryStorage();
            var userState = new UserState(storage);
            var context = TestUtilities.CreateEmptyContext();
            await userState.LoadAsync(context);
            userState.SetValue("validKey", "testValue");

            // Act
            var hasValue = userState.HasValue("validKey");
            var hasNonExistent = userState.HasValue("nonExistent");

            // Assert
            Assert.True(hasValue);
            Assert.False(hasNonExistent);
        }

        [Fact]
        public async Task DeleteValue_WithValidName_ShouldSucceed()
        {
            // Arrange
            var storage = new MemoryStorage();
            var userState = new UserState(storage);
            var context = TestUtilities.CreateEmptyContext();
            await userState.LoadAsync(context);
            userState.SetValue("validKey", "testValue");

            // Act
            userState.DeleteValue("validKey");

            // Assert
            Assert.False(userState.HasValue("validKey"));
        }

        [Fact]
        public async Task SetValue_WithValidName_ShouldSucceed()
        {
            // Arrange
            var storage = new MemoryStorage();
            var userState = new UserState(storage);
            var context = TestUtilities.CreateEmptyContext();
            await userState.LoadAsync(context);

            // Act
            userState.SetValue("validKey", "testValue");

            // Assert
            Assert.Equal("testValue", userState.GetValue<string>("validKey"));
        }

        [Fact]
        public async Task TryGetValue_WithNullName_ShouldThrow()
        {
            // Arrange
            var storage = new MemoryStorage();
            var userState = new UserState(storage);
            var context = TestUtilities.CreateEmptyContext();
            await userState.LoadAsync(context);

            // Act & Assert - Dictionary.ContainsKey throws ArgumentNullException for null
            Assert.Throws<ArgumentNullException>(() => userState.TryGetValue<string>(null, out _));
        }

        [Fact]
        public async Task ProtectedMethods_WithNullName_ShouldThrow()
        {
            // Arrange
            var storage = new MemoryStorage();
            var testState = new TestAgentState(storage);
            var context = TestUtilities.CreateEmptyContext();
            await testState.LoadAsync(context);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => testState.CallGetPropertyValue<string>(null));
            Assert.Throws<ArgumentNullException>(() => testState.CallSetPropertyValue(null, "value"));
            Assert.Throws<ArgumentNullException>(() => testState.CallDeletePropertyValue(null));
        }

        [Fact]
        public async Task ProtectedMethods_WithEmptyName_ShouldThrow()
        {
            // Arrange
            var storage = new MemoryStorage();
            var testState = new TestAgentState(storage);
            var context = TestUtilities.CreateEmptyContext();
            await testState.LoadAsync(context);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => testState.CallGetPropertyValue<string>(string.Empty));
            Assert.Throws<ArgumentException>(() => testState.CallSetPropertyValue(string.Empty, "value"));
            Assert.Throws<ArgumentException>(() => testState.CallDeletePropertyValue(string.Empty));
        }

        [Fact]
        public async Task ProtectedMethods_WithValidName_ShouldSucceed()
        {
            // Arrange
            var storage = new MemoryStorage();
            var testState = new TestAgentState(storage);
            var context = TestUtilities.CreateEmptyContext();
            await testState.LoadAsync(context);

            // Act
            testState.CallSetPropertyValue("key", "value");
            var value = testState.CallGetPropertyValue<string>("key");

            // Assert
            Assert.Equal("value", value);
        }

        /// <summary>
        /// Test subclass to expose protected methods for testing.
        /// </summary>
        private class TestAgentState : AgentState
        {
            public TestAgentState(IStorage storage) : base(storage, "test")
            {
            }

            protected override string GetStorageKey(ITurnContext turnContext)
            {
                return $"test/{turnContext.Activity.ChannelId}/conversations/{turnContext.Activity.Conversation?.Id}";
            }

            public T CallGetPropertyValue<T>(string propertyName)
            {
                return GetPropertyValue<T>(propertyName);
            }

            public void CallSetPropertyValue(string propertyName, object value)
            {
                SetPropertyValue(propertyName, value);
            }

            public void CallDeletePropertyValue(string propertyName)
            {
                DeletePropertyValue(propertyName);
            }
        }
    }
}
