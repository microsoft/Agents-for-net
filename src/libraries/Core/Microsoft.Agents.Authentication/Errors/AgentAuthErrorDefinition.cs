namespace Microsoft.Agents.Authentication.Errors
{
    /// <summary>
    /// This class describes the error definition, duplicated from Core as Authentication core is as standalone lib. 
    /// </summary>
    /// <param name="code">Error code for the exception</param>
    /// <param name="description">Displayed Error message</param>
    /// <param name="helplink">Help URL Link for the Error.</param>
    internal record AgentAuthErrorDefinition(int code, string description, string helplink);
}
