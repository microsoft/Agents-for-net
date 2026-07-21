// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.Graph;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.MSTeams;

/// <summary>
/// Provides Teams-specific helpers for working with the current <see cref="ITurnContext"/>.
/// </summary>
public interface ITeamsTurnContext : ITurnContext
{
    /// <summary>
    /// Returns the ApiClient instance registered for Microsoft Teams API access in the current turn context.
    /// </summary>
    Microsoft.Teams.Api.Clients.ApiClient Client { get; }

    /// <summary>
    /// Sends an activity to the conversation with a targeted treatment, allowing the activity to be directed to a
    /// specific recipient or group within the conversation.
    /// </summary>
    /// <param name="activity">The activity to send. Must represent the message or event to be delivered and cannot be null.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the send operation.</param>
    /// <returns>A task that represents the asynchronous send operation. The task result contains a ResourceResponse with
    /// information about the sent activity.</returns>
    Task<ResourceResponse> SendTargetedActivityAsync(IActivity activity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a set of activities to targeted recipients within the current turn context asynchronously.
    /// </summary>
    /// <param name="activities">An array of activities to send. Each activity will be treated as targeted. Cannot be null and must not
    /// contain null elements.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the send operation.</param>
    /// <returns>A task that represents the asynchronous send operation. The task result contains an array of
    /// ResourceResponse objects for each sent activity.</returns>
    Task<ResourceResponse[]> SendTargetedActivitiesAsync(IActivity[] activities, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a <see cref="GraphServiceClient"/> authenticated with a delegated (user) token for the signed-in user
    /// of the current turn.
    /// </summary>
    /// <remarks>Requires that user authorization is configured for the agent and the user is signed in. The returned
    /// client makes requests to Microsoft Graph on behalf of the user.</remarks>
    /// <param name="handlerName">The name of the sign-in handler to use for token acquisition. If null, the default handler is used.</param>
    /// <param name="graphBaseUrl">The base URL for the Microsoft Graph API. Defaults to "https://graph.microsoft.com/v1.0".</param>
    /// <returns>A <see cref="GraphServiceClient"/> authenticated with a delegated user token.</returns>
    GraphServiceClient GetGraphClient(string handlerName = null, string graphBaseUrl = "https://graph.microsoft.com/v1.0");

    /// <summary>
    /// Creates a <see cref="GraphServiceClient"/> authenticated with an <b>app-only</b> (application) token, using the
    /// token connection resolved for the current turn.
    /// </summary>
    /// <remarks>Unlike <see cref="GetGraphClient(string, string)"/>, this method uses application permissions, so the
    /// caller must specify the target resource in the request path (for example <c>client.Users["{userId}"]...</c>).
    /// No configuration beyond the agent's existing token connection is required.</remarks>
    /// <param name="graphBaseUrl">The base URL for the Microsoft Graph API. Defaults to "https://graph.microsoft.com/v1.0".</param>
    /// <returns>A <see cref="GraphServiceClient"/> authenticated with an app-only token.</returns>
    GraphServiceClient GetAppGraphClient(string graphBaseUrl = "https://graph.microsoft.com/v1.0");

    /// <summary>
    /// Creates a <see cref="GraphServiceClient"/> authenticated with an <b>app-only</b> (application) token, using the
    /// named token connection.
    /// </summary>
    /// <remarks>The returned client uses application permissions, so the caller must specify the target resource in
    /// the request path (for example <c>client.Users["{userId}"]...</c>).</remarks>
    /// <param name="connectionName">The name of the token connection whose credentials acquire the app-only token. Cannot be null or empty.</param>
    /// <param name="graphBaseUrl">The base URL for the Microsoft Graph API. Defaults to "https://graph.microsoft.com/v1.0".</param>
    /// <returns>A <see cref="GraphServiceClient"/> authenticated with an app-only token.</returns>
    GraphServiceClient GetAppGraphClientForConnection(string connectionName, string graphBaseUrl = "https://graph.microsoft.com/v1.0");
}
