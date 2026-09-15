
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
    internal static readonly AgentErrorDefinition AuthorizationSecuritySchemeNameRequired =
        new(-100007, Resource(nameof(AuthorizationSecuritySchemeNameRequired)), $"{HelpLinkBase}-100007");
    internal static readonly AgentErrorDefinition AuthorizationExactlyOneOAuthFlowRequired =
        new(-100008, Resource(nameof(AuthorizationExactlyOneOAuthFlowRequired)), $"{HelpLinkBase}-100008");
    internal static readonly AgentErrorDefinition AuthorizationSecuritySchemeConflict =
        new(-100009, Resource(nameof(AuthorizationSecuritySchemeConflict)), $"{HelpLinkBase}-100009");
    internal static readonly AgentErrorDefinition AgentCardNullSecurityScheme =
        new(-100010, Resource(nameof(AgentCardNullSecurityScheme)), $"{HelpLinkBase}-100010");
    internal static readonly AgentErrorDefinition AgentCardDuplicateSecurityScheme =
        new(-100011, Resource(nameof(AgentCardDuplicateSecurityScheme)), $"{HelpLinkBase}-100011");
    internal static readonly AgentErrorDefinition AgentCardConflictingSkillRegistration =
        new(-100012, Resource(nameof(AgentCardConflictingSkillRegistration)), $"{HelpLinkBase}-100012");
    internal static readonly AgentErrorDefinition AgentCardUnknownAuthorizationHandler =
        new(-100013, Resource(nameof(AgentCardUnknownAuthorizationHandler)), $"{HelpLinkBase}-100013");
    internal static readonly AgentErrorDefinition AgentCardAuthorizationMetadataRequired =
        new(-100014, Resource(nameof(AgentCardAuthorizationMetadataRequired)), $"{HelpLinkBase}-100014");
    internal static readonly AgentErrorDefinition AgentCardProtectedProperty =
        new(-100015, Resource(nameof(AgentCardProtectedProperty)), $"{HelpLinkBase}-100015");
    internal static readonly AgentErrorDefinition AgentCardMissingSecurityScheme =
        new(-100016, Resource(nameof(AgentCardMissingSecurityScheme)), $"{HelpLinkBase}-100016");
    internal static readonly AgentErrorDefinition AgentCardUndefinedScope =
        new(-100017, Resource(nameof(AgentCardUndefinedScope)), $"{HelpLinkBase}-100017");
    internal static readonly AgentErrorDefinition AgentRequestContextMissing =
        new(-100018, Resource(nameof(AgentRequestContextMissing)), $"{HelpLinkBase}-100018");
    internal static readonly AgentErrorDefinition BlobTaskWriteConflict =
        new(-100019, Resource(nameof(BlobTaskWriteConflict)), $"{HelpLinkBase}-100019");
}
