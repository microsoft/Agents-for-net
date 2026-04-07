// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Microsoft.Agents.Builder.Telemetry.Authorization.Scopes;
using Microsoft.Agents.Core.Telemetry;
using Xunit;

namespace Microsoft.Agents.Builder.Tests.Telemetry
{
    [Collection("TelemetryTests")]
    public class AuthorizationScopeTests : TelemetryScopeTestBase
    {
        #region ScopeAgenticToken

        [Fact]
        public void ScopeAgenticToken_CreatesActivity_WithCorrectName()
        {
            using var scope = new ScopeAgenticToken("handler-1", null, null);

            var started = Assert.Single(StartedActivities);
            Assert.Equal("agents.authorization.agentic_token", started.OperationName);
        }

        [Fact]
        public void ScopeAgenticToken_Callback_SetsAuthHandlerIdTag()
        {
            var scope = new ScopeAgenticToken("handler-abc", null, null);
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("handler-abc", stopped.GetTagItem(TagNames.AuthHandlerId));
        }

        [Fact]
        public void ScopeAgenticToken_Callback_SetsExchangeConnectionTag_WhenProvided()
        {
            var scope = new ScopeAgenticToken("handler-1", "my-connection", null);
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("my-connection", stopped.GetTagItem(TagNames.ExchangeConnection));
        }

        [Fact]
        public void ScopeAgenticToken_Callback_SetsAuthScopesTag_WhenProvided()
        {
            var scope = new ScopeAgenticToken("handler-1", null, new[] { "scope-a", "scope-b" });
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("scope-a,scope-b", stopped.GetTagItem(TagNames.AuthScopes));
        }

        [Fact]
        public void ScopeAgenticToken_Callback_DoesNotSetExchangeConnection_WhenNull()
        {
            var scope = new ScopeAgenticToken("handler-1", null, null);
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Null(stopped.GetTagItem(TagNames.ExchangeConnection));
        }

        [Fact]
        public void ScopeAgenticToken_SetError_SetsErrorStatus()
        {
            var scope = new ScopeAgenticToken("handler-1", null, null);
            scope.SetError(new InvalidOperationException("agentic token error"));
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal(System.Diagnostics.ActivityStatusCode.Error, stopped.Status);
        }

        #endregion

        #region ScopeAzureBotToken

        [Fact]
        public void ScopeAzureBotToken_CreatesActivity_WithCorrectName()
        {
            using var scope = new ScopeAzureBotToken("handler-1", null, null);

            var started = Assert.Single(StartedActivities);
            Assert.Equal("agents.authorization.azure_bot_token", started.OperationName);
        }

        [Fact]
        public void ScopeAzureBotToken_Callback_SetsAllTags()
        {
            var scope = new ScopeAzureBotToken("handler-xyz", "conn-1", new[] { "s1" });
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("handler-xyz", stopped.GetTagItem(TagNames.AuthHandlerId));
            Assert.Equal("conn-1", stopped.GetTagItem(TagNames.ExchangeConnection));
            Assert.Equal("s1", stopped.GetTagItem(TagNames.AuthScopes));
        }

        [Fact]
        public void ScopeAzureBotToken_Callback_DoesNotSetScopesOrConnection_WhenNull()
        {
            var scope = new ScopeAzureBotToken("handler-1", null, null);
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Null(stopped.GetTagItem(TagNames.ExchangeConnection));
            Assert.Null(stopped.GetTagItem(TagNames.AuthScopes));
        }

        [Fact]
        public void ScopeAzureBotToken_SetError_SetsErrorStatus()
        {
            var scope = new ScopeAzureBotToken("handler-1", null, null);
            scope.SetError(new InvalidOperationException("bot token error"));
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal(System.Diagnostics.ActivityStatusCode.Error, stopped.Status);
        }

        #endregion

        #region ScopeAzureBotSignIn

        [Fact]
        public void ScopeAzureBotSignIn_CreatesActivity_WithCorrectName()
        {
            using var scope = new ScopeAzureBotSignIn("handler-1", null, null);

            var started = Assert.Single(StartedActivities);
            Assert.Equal("agents.authorization.azure_bot_sign_in", started.OperationName);
        }

        [Fact]
        public void ScopeAzureBotSignIn_Callback_SetsAuthHandlerIdAndConnectionTags()
        {
            var scope = new ScopeAzureBotSignIn("handler-sign", "oauth-conn", null);
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("handler-sign", stopped.GetTagItem(TagNames.AuthHandlerId));
            Assert.Equal("oauth-conn", stopped.GetTagItem(TagNames.ExchangeConnection));
        }

        [Fact]
        public void ScopeAzureBotSignIn_Callback_DoesNotSetScopesOrConnection_WhenNull()
        {
            var scope = new ScopeAzureBotSignIn("handler-1", null, null);
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Null(stopped.GetTagItem(TagNames.ExchangeConnection));
            Assert.Null(stopped.GetTagItem(TagNames.AuthScopes));
        }

        [Fact]
        public void ScopeAzureBotSignIn_SetError_SetsErrorStatus()
        {
            var scope = new ScopeAzureBotSignIn("handler-1", null, null);
            scope.SetError(new InvalidOperationException("sign-in error"));
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal(System.Diagnostics.ActivityStatusCode.Error, stopped.Status);
        }

        #endregion

        #region ScopeAzureBotSignOut

        [Fact]
        public void ScopeAzureBotSignOut_CreatesActivity_WithCorrectName()
        {
            using var scope = new ScopeAzureBotSignOut("handler-1");

            var started = Assert.Single(StartedActivities);
            Assert.Equal("agents.authorization.azure_bot_sign_out", started.OperationName);
        }

        [Fact]
        public void ScopeAzureBotSignOut_Callback_SetsAuthHandlerIdTag()
        {
            var scope = new ScopeAzureBotSignOut("handler-out");
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("handler-out", stopped.GetTagItem(TagNames.AuthHandlerId));
        }

        [Fact]
        public void ScopeAzureBotSignOut_Callback_DoesNotSetExchangeConnection()
        {
            var scope = new ScopeAzureBotSignOut("handler-1");
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Null(stopped.GetTagItem(TagNames.ExchangeConnection));
        }

        [Fact]
        public void ScopeAzureBotSignOut_SetError_SetsErrorStatus()
        {
            var scope = new ScopeAzureBotSignOut("handler-1");
            scope.SetError(new InvalidOperationException("sign-out error"));
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal(System.Diagnostics.ActivityStatusCode.Error, stopped.Status);
        }

        #endregion
    }
}
