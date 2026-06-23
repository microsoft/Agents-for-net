// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Extensions.MSTeams.Config;

/// <summary>
/// Provides routing for Microsoft Teams bot config interactions.
/// </summary>
/// <remarks>
/// <para>
/// Teams allows users to configure a bot directly from its description card by clicking the
/// settings (gear) icon. The config flow is:
/// </para>
/// <list type="number">
///   <item>Register fetch and submit handlers via <see cref="OnConfigFetch"/> and <see cref="OnConfigSubmit"/>.</item>
///   <item>On fetch, return a <see cref="Microsoft.Teams.Api.Config.ConfigTaskResponse"/> containing a
///     <see cref="Microsoft.Teams.Api.TaskModules.ContinueTask"/> with a <see cref="Microsoft.Teams.Api.TaskModules.TaskInfo"/>
///     that holds the config adaptive card.</item>
///   <item>When the user submits the form, the submit handler receives the form data via <c>configData</c>.
///     Return a <see cref="Microsoft.Teams.Api.Config.ConfigTaskResponse"/> containing a
///     <see cref="Microsoft.Teams.Api.TaskModules.MessageTask"/> to close the config pane.</item>
/// </list>
/// <example>
/// The following example handles fetch and submit using route attributes.
/// <code>
/// [TeamsExtension]
/// public partial class MyAgent(AgentApplicationOptions options) : AgentApplication(options)
/// {
///     [ConfigFetchRoute]
///     public Task&lt;Microsoft.Teams.Api.Config.ConfigResponse&gt; OnConfigFetchAsync(
///         ITeamsTurnContext turnContext,
///         ITurnState turnState,
///         object configData,
///         CancellationToken cancellationToken)
///     {
///         const string cardJson = """
///             {
///                 "type": "AdaptiveCard",
///                 "version": "1.4",
///                 "body": [
///                     { "type": "TextBlock", "text": "Bot Config", "weight": "bolder" },
///                     { "type": "Input.Text", "id": "setting", "label": "Setting", "placeholder": "Enter a value" }
///                 ],
///                 "actions": [
///                     { "type": "Action.Submit", "title": "Save" }
///                 ]
///             }
///             """;
///
///         var response = new Microsoft.Teams.Api.Config.ConfigTaskResponse(
///             new Microsoft.Teams.Api.TaskModules.ContinueTask(
///                 new Microsoft.Teams.Api.TaskModules.TaskInfo
///                 {
///                     Title = "Configure Bot",
///                     Height = new Microsoft.Teams.Common.Union&lt;int, Microsoft.Teams.Api.TaskModules.Size&gt;(300),
///                     Width = new Microsoft.Teams.Common.Union&lt;int, Microsoft.Teams.Api.TaskModules.Size&gt;(400),
///                     Card = new Microsoft.Agents.Core.Models.Attachment
///                     {
///                         ContentType = "application/vnd.microsoft.card.adaptive",
///                         Content = System.Text.Json.JsonSerializer.Deserialize&lt;System.Text.Json.JsonElement&gt;(cardJson)
///                     }
///                 }));
///
///         return Task.FromResult&lt;Microsoft.Teams.Api.Config.ConfigResponse&gt;(response);
///     }
///
///     [ConfigSubmitRoute]
///     public Task&lt;Microsoft.Teams.Api.Config.ConfigResponse&gt; OnConfigSubmitAsync(
///         ITeamsTurnContext turnContext,
///         ITurnState turnState,
///         object configData,
///         CancellationToken cancellationToken)
///     {
///         // configData contains the submitted form values
///         var response = new Microsoft.Teams.Api.Config.ConfigTaskResponse(
///             new Microsoft.Teams.Api.TaskModules.MessageTask("Config saved!"));
///
///         return Task.FromResult&lt;Microsoft.Teams.Api.Config.ConfigResponse&gt;(response);
///     }
/// }
/// </code>
/// </example>
/// </remarks>
public class TeamsConfig
{
    private readonly AgentApplication _app;
    private readonly ChannelId _channelId;

    internal TeamsConfig(AgentApplication app, ChannelId channelId)
    {
        _app = app;
        _channelId = channelId;
    }

    /// <summary>
    /// Handles config fetch events for Microsoft Teams.
    /// </summary>
    /// <remarks>Alternatively, the <see cref="ConfigFetchRouteAttribute"/> can be used to decorate a <see cref="ConfigHandler"/> method for the same purpose.</remarks>
    /// <param name="handler">Function to call when the event is triggered.</param>
    /// <param name="autoSignInHandlers">OAuth sign-in handler names for automatic sign-in before the route handler is invoked. Specify <see langword="null"/> to skip automatic sign-in.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <returns>The AgentExtension instance for chaining purposes.</returns>
    public TeamsConfig OnConfigFetch(ConfigHandler handler, string[] autoSignInHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        _app.AddRoute(ConfigFetchRouteBuilder.Create()
            .WithHandler(handler)
            .WithChannelId(_channelId)
            .WithOrderRank(rank)
            .WithOAuthHandlers(autoSignInHandlers)
            .Build());
        return this;
    }

    /// <summary>
    /// Handles config submit events for Microsoft Teams.
    /// </summary>
    /// <remarks>Alternatively, the <see cref="ConfigSubmitRouteAttribute"/> can be used to decorate a <see cref="ConfigHandler"/> method for the same purpose.</remarks>
    /// <param name="handler">Function to call when the event is triggered.</param>
    /// <param name="autoSignInHandlers">OAuth sign-in handler names for automatic sign-in before the route handler is invoked. Specify <see langword="null"/> to skip automatic sign-in.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <returns>The AgentExtension instance for chaining purposes.</returns>
    public TeamsConfig OnConfigSubmit(ConfigHandler handler, string[] autoSignInHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        _app.AddRoute(ConfigSubmitRouteBuilder.Create()
            .WithHandler(handler)
            .WithChannelId(_channelId)
            .WithOrderRank(rank)
            .WithOAuthHandlers(autoSignInHandlers)
            .Build());
        return this;
    }

}
