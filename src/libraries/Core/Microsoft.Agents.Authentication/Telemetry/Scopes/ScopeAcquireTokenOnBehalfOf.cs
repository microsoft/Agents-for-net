using Microsoft.Agents.Core.Telemetry;
using System;
using System.Collections.Generic;

#nullable enable

namespace Microsoft.Agents.Authentication.Telemetry.Scopes
{
    /// <summary>
    /// A <see cref="ScopeTokenRequest"/> that traces an On-Behalf-Of token acquisition
    /// and records the requested scopes.
    /// </summary>
    internal class ScopeAcquireTokenOnBehalfOf : ScopeTokenRequest
    {
        private readonly IEnumerable<string> _scopes;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScopeAcquireTokenOnBehalfOf"/> class.
        /// </summary>
        /// <param name="scopes">The OAuth/OIDC scopes requested for the On-Behalf-Of token.</param>
        public ScopeAcquireTokenOnBehalfOf(IEnumerable<string> scopes) : base(Constants.ScopeAcquireTokenOnBehalfOf, Constants.AuthMethodOBO)
        {
            _scopes = scopes;
        }

        /// <inheritdoc />
        /// <remarks>
        /// Calls <c>base.Callback</c> to record the auth method, then tags the activity with
        /// <see cref="TagNames.AuthScopes"/>.
        /// </remarks>
        protected override void Callback(System.Diagnostics.Activity telemetryActivity, double duration, Exception? error)
        {
            base.Callback(telemetryActivity, duration, error);
            telemetryActivity.SetTag(TagNames.AuthScopes, TelemetryUtils.FormatScopes(_scopes));
        }
    }
}