using System;

namespace Microsoft.Agents.Core.Errors
{
    /// <summary>
    /// This class describes the error definition
    /// </summary>
    /// <param name="code">Error code for the exception</param>
    /// <param name="description">Displayed Error message</param>
    /// <param name="helplink">Help URL Link for the Error.</param>
    public record AgentErrorDefinition(int code, string description, string helplink);

    public static class ExceptionHelper
    {
        public static T GenerateException<T>(AgentErrorDefinition errorDefinition, Exception innerException, params string[] messageFormat) where T : Exception
        {
            var excp = (T)Activator.CreateInstance(typeof(T), new object[] { string.Format(errorDefinition.description, messageFormat), innerException });
            excp.HResult = errorDefinition.code;
            excp.HelpLink = errorDefinition.helplink;
            return excp;
        }
    }
}
