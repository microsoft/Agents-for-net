namespace Microsoft.Agents.Authentication.Telemetry
{
    internal static class Constants
    {
        internal static readonly string ScopeGetAccessToken = "agents.auth.get_access_token";
        internal static readonly string ScopeAcquireTokenOnBehalfOf = "agents.auth.acquire_token_on_behalf_of";
        internal static readonly string ScopeGetAgenticInstanceToken = "agents.auth.get_agentic_instance_token";
        internal static readonly string ScopeGetAgenticUserToken = "agents.auth.get_agentic_user_token";

        internal static readonly string MetricTokenRequestDuration = "agents.auth.token.request.duration";
        internal static readonly string MetricTokenRequestCount = "agents.auth.token.request.count";

        internal static readonly string AuthMethodOBO = "obo";
        internal static readonly string AuthMethodAgenticInstance = "agentic_instance";
        internal static readonly string AuthMethodAgenticUser = "agentic_user";
    }
}
