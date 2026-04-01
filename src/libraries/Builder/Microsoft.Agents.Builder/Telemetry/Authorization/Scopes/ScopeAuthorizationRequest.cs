using Microsoft.Agents.Core.Telemetry;
using System;
using System.Collections.Generic;
using System.Diagnostics;

#nullable enable

namespace Microsoft.Agents.Builder.Telemetry.Authorization.Scopes
{
    internal class ScopeAuthorizationRequest : TelemetryScope
    {
        private readonly string _authHandlerId;
        private readonly string? _exchangeConnection;
        private readonly IEnumerable<string>? _scopes;

        public ScopeAuthorizationRequest(string scopeName, string authHandlerId, string? exchangeConnection = null, IEnumerable<string>? scopes = null) : base(scopeName)
        {
            _authHandlerId = authHandlerId;
            _exchangeConnection = exchangeConnection;
            _scopes = scopes;
        }

        protected override void Callback(Activity telemetryActivity, double duration, Exception? exception)
        {
            telemetryActivity.SetTag(TagNames.AuthHandlerId, _authHandlerId);
            if (_exchangeConnection != null)
            {
                telemetryActivity.SetTag(TagNames.ExchangeConnection, _exchangeConnection);
            }
            if (_scopes != null)
            {
                telemetryActivity.SetTag(TagNames.AuthScopes, TelemetryUtils.FormatScopes(_scopes));
            }
        }
        }
    }
