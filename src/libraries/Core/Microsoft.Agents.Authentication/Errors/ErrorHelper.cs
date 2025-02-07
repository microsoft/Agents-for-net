namespace Microsoft.Agents.Authentication.Errors
{
    /// <summary>
    /// Error helper for the Authentication core system
    /// This is used to setup the localized error codes for the authentication subsystem of the AgentSDK
    /// 
    /// Note: specific auth providers are expected to implement their own error codes inside their own libraries. 
    /// </summary>
    internal static class ErrorHelper
    {
        /// <summary>
        /// Base error code for the authentication provider
        /// </summary>
        private static int baseAuthProviderErrorCode = -40000;

        internal static AgentAuthErrorDefinition MissingAuthenticationConfiguration = new AgentAuthErrorDefinition(baseAuthProviderErrorCode, Properties.Resources.MissingAuthenticationConfig, "https://aka.ms/AgentsSDK-DotNetMSALAuth");

    }

}
