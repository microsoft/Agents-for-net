// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Errors;

namespace Microsoft.Agents.Authentication.Errors
{
    /// <summary>
    /// Error helper for the Authentication core system
    /// This is used to setup the localized error codes for the authentication subsystem of the AgentSDK
    /// 
    /// Note: specific auth providers are expected to implement their own error codes inside their own libraries. 
    /// 
    /// Each Error should be created as as an AgentAuthErrorDefinition and added to the ErrorHelper class
    /// Each definition should include an error code as a - from the base error code, a description sorted in the Resource.resx file to support localization, and a help link pointing to an AKA link to get help for the given error. 
    /// 
    /// when used, there are 2 methods.. 
    /// Method 1: 
    ///     throw new IndexOutOfRangeException(ErrorHelper.MissingAuthenticationConfiguration.description)
    ///     {
    ///         HResult = ErrorHelper.MissingAuthenticationConfiguration.code,
    ///         HelpLink = ErrorHelper.MissingAuthenticationConfiguration.helplink
    ///     };
    /// 
/// </summary>
internal static class ErrorHelper
    {
        /// <summary>
        /// Base error code for the authentication provider
        /// </summary>
        private static int baseAuthProviderErrorCode = -40000;

        internal static AgentErrorDefinition MissingAuthenticationConfiguration = new(baseAuthProviderErrorCode, Properties.Resources.Error_MissingAuthenticationConfig, "https://aka.ms/AgentsSDK-DotNetMSALAuth");
        internal static AgentErrorDefinition ConnectionNotFoundByName = new(baseAuthProviderErrorCode - 1, Properties.Resources.Error_ConnectionNotFoundByName, "https://aka.ms/AgentsSDK-DotNetMSALAuth");
        internal static AgentErrorDefinition FailedToCreateAuthModuleProvider = new(baseAuthProviderErrorCode - 2, Properties.Resources.Error_FailedToCreateAuthModuleProvider, "https://aka.ms/AgentsSDK-DotNetMSALAuth");
        internal static AgentErrorDefinition AuthProviderTypeNotFound = new(baseAuthProviderErrorCode - 3, Properties.Resources.Error_AuthModuleNotFound, "https://aka.ms/AgentsSDK-DotNetMSALAuth");
        internal static AgentErrorDefinition AuthProviderTypeInvalidConstructor = new(baseAuthProviderErrorCode - 4, Properties.Resources.Error_InvalidAuthProviderConstructor, "https://aka.ms/AgentsSDK-DotNetMSALAuth");
        internal static AgentErrorDefinition ConfigurationSectionNotFound = new(baseAuthProviderErrorCode - 5, Properties.Resources.Error_ConfigurationSectionNotFound, "https://aka.ms/AgentsSDK-DotNetMSALAuth");
        internal static AgentErrorDefinition ConfigurationSectionNotProvided = new(baseAuthProviderErrorCode - 6, Properties.Resources.Error_ConfigurationSectionNotProvided, "https://aka.ms/AgentsSDK-DotNetMSALAuth");

    }
}
