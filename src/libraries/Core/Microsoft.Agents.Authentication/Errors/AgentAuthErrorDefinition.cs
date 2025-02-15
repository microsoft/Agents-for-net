using System;

namespace Microsoft.Agents.Authentication.Errors
{
    /// <summary>
    /// This class describes the error definition, duplicated from Core as Authentication core is as standalone lib. 
    /// 
    /// Each Error should be created as as an AgentAuthErrorDefinition and added to the ErrorHelper class
    /// Each definition should include an error code as a - from the base error code, a description sorted in the Resource.resx file to support localization, and a help link pointing to an AKA link to get help for the given error. 
    /// 
    /// 
    /// when used, there are is 1 method in the authentication class.. 
    /// Method 1: 
    ///     throw new IndexOutOfRangeException(ErrorHelper.MissingAuthenticationConfiguration.description)
    ///     {
    ///         HResult = ErrorHelper.MissingAuthenticationConfiguration.code,
    ///         HelpLink = ErrorHelper.MissingAuthenticationConfiguration.helplink
    ///     };
    ///
    /// </summary>
    /// <param name="code">Error code for the exception</param>
    /// <param name="description">Displayed Error message</param>
    /// <param name="helplink">Help URL Link for the Error.</param>
    internal record AgentAuthErrorDefinition(int code, string description, string helplink);

    internal static class ExceptionHelper
    {
        public static T GenerateException<T>(AgentAuthErrorDefinition errorDefinition, Exception innerException, params string[] messageFormat) where T : Exception
        {
            var excp = (T)Activator.CreateInstance(typeof(T), new object[] { string.Format(errorDefinition.description, messageFormat), innerException });
            excp.HResult = errorDefinition.code;
            excp.HelpLink = errorDefinition.helplink;
            return excp;
        }
    }

}
