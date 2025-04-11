// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Core.Errors
{
    /// <summary>
    /// This class describes the error definition
    /// 
    /// Each Error should be created as as an AgentAuthErrorDefinition and added to the ErrorHelper class
    /// Each definition should include an error code as a - from the base error code, a description sorted in the Resource.resx file to support localization, and a help link pointing to an AKA link to get help for the given error. 
    /// 
    /// 
    /// when used, there are is 2 methods in used in the general space. 
    /// Method 1: 
    /// Throw a new exception with the error code, description and helplink
    ///     throw new IndexOutOfRangeException(ErrorHelper.MissingAuthenticationConfiguration.description)
    ///     {
    ///         HResult = ErrorHelper.MissingAuthenticationConfiguration.code,
    ///         HelpLink = ErrorHelper.MissingAuthenticationConfiguration.helplink
    ///     };
    ///
    /// Method 2: 
    /// 
    ///     throw Microsoft.Agents.Core.Errors.ExceptionHelper.GenerateException&lt;OperationCanceledException&gt;(
    ///         ErrorHelper.NullIAccessTokenProvider, ex, $"{AgentClaims.GetAppId(claimsIdentity)}:{serviceUrl}");
    /// 
    /// </summary>
    /// <param name="code">Error code for the exception</param>
    /// <param name="description">Displayed Error message</param>
    /// <param name="helplink">Help URL Link for the Error.</param>
    public record AgentErrorDefinition(int code, string description, string helplink);

    public static class ExceptionHelper
    {
        public static T GenerateException<T>(AgentErrorDefinition errorDefinition, Exception innerException, params string[] messageFormat) where T : Exception
        {
            var excp = innerException != null
                ? (T)Activator.CreateInstance(typeof(T), new object[] { string.Format(errorDefinition.description, messageFormat), innerException })
                : (T)Activator.CreateInstance(typeof(T), new object[] { string.Format(errorDefinition.description, messageFormat) });

            excp.HResult = errorDefinition.code;
            excp.HelpLink = errorDefinition.helplink;
            return excp;
        }
    }
}
