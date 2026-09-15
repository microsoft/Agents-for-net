
using Microsoft.Agents.Core.Errors;

namespace Microsoft.Agents.Extensions.A2A.Errors;

/// <summary>
/// Defines localized Agent SDK error metadata for failures owned by the A2A extension.
/// </summary>
internal static partial class ErrorHelper
{
    // Base error code for the builder: -100000

    private const string HelpLinkBase = "https://aka.ms/M365AgentsErrorCodes/#";

    private static string Resource(string name) =>
        Properties.Resources.ResourceManager.GetString(name, Properties.Resources.Culture) ?? name;

    internal static readonly AgentErrorDefinition UnexpectedTokenExpiration =
        new(-100000, Properties.Resources.UnexpectedTokenExpiration, $"{HelpLinkBase}-100000");
    internal static readonly AgentErrorDefinition UnexpectedRequestToken =
        new(-100001, Properties.Resources.UnexpectedRequestToken, $"{HelpLinkBase}-100001");
    internal static readonly AgentErrorDefinition ConflictingSkillMetadata =
        new(-100002, Resource(nameof(ConflictingSkillMetadata)), $"{HelpLinkBase}-100002");
    internal static readonly AgentErrorDefinition SkillRouteMissing =
        new(-100003, Resource(nameof(SkillRouteMissing)), $"{HelpLinkBase}-100003");
    internal static readonly AgentErrorDefinition SkillRouteAlreadyDefined =
        new(-100004, Resource(nameof(SkillRouteAlreadyDefined)), $"{HelpLinkBase}-100004");
    internal static readonly AgentErrorDefinition AgentApplicationNotFound =
        new(-100005, Resource(nameof(AgentApplicationNotFound)), $"{HelpLinkBase}-100005");
    internal static readonly AgentErrorDefinition AgentInterfaceMissing =
        new(-100006, Resource(nameof(AgentInterfaceMissing)), $"{HelpLinkBase}-100006");
}
