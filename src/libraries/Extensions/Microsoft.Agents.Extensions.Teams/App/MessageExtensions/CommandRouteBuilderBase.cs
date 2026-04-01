// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.Errors;
using System;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.App.MessageExtensions;

/// <summary>
/// Provides a base builder for configuring message extension routes for MessageExtensions that handle command-based Invoke activities 
/// in an AgentApplication. This builder allows for defining command matching logic using either exact string matches or regular expression 
/// patterns, enabling flexible routing based on the command specified in the incoming activity. The builder ensures that the route is 
/// properly configured for Invoke routing and validates required properties before building the route.
/// </summary>
/// <typeparam name="TBuilder">The type of the builder that extends the functionality of the CommandRouteBuilderBase, enabling fluent
/// configuration.</typeparam>
public class CommandRouteBuilderBase<TBuilder> : RouteBuilderBase<TBuilder> where TBuilder : CommandRouteBuilderBase<TBuilder>
{
    private Func<string, bool> _commandMatch;

    protected string InvokeName { get; set; }
    protected string? PreviewAction { get; set; }

    public CommandRouteBuilderBase() : base()
    {
        _route.Flags |= RouteFlags.Invoke;
    }

    /// <summary>
    /// Match a specific command name.
    /// </summary>
    /// <param name="command">The command string to be matched. This parameter cannot be null or whitespace.</param>
    /// <returns>The current instance of the builder, allowing for method chaining.</returns>
    /// <exception cref="InvalidOperationException">Command has already been defined for this builder
    /// instance.</exception>
    public TBuilder WithCommand(string command)
    {
        AssertionHelpers.ThrowIfNullOrWhiteSpace(command, nameof(command));

        if (_commandMatch != null)
        {
            throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"{typeof(TBuilder).Name}.WithCommand({command})");
        }

        _commandMatch = (input) => string.Equals(command, input);
        return (TBuilder)this;
    }

    /// <summary>
    /// Match a specific command name pattern.
    /// </summary>
    /// <param name="commandPattern">The command Regex pattern to be matched. This parameter cannot be null.</param>
    /// <returns>The current instance of the builder, allowing for method chaining.</returns>
    /// <exception cref="InvalidOperationException">Command has already been defined for this builder
    /// instance.</exception>
    public TBuilder WithCommand(Regex commandPattern)
    {
        AssertionHelpers.ThrowIfNull(commandPattern, nameof(commandPattern));

        if (_commandMatch != null)
        {
            throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"{typeof(TBuilder).Name}.WithCommand(Regex({commandPattern}))");
        }

        _commandMatch = (string input) => commandPattern.IsMatch(input);
        return (TBuilder)this;
    }

    /// <summary>
    /// Returns the current route builder instance configured for Invoke routing. This method ensures that the route
    /// remains set as an Invoke route.
    /// </summary>
    /// <remarks>This override prevents changing the route configuration from Invoke routing,
    /// maintaining consistency with the route's initial setup.</remarks>
    /// <param name="isInvoke">A value indicating whether the route should be treated as an Invoke route. The parameter is ignored, as the
    /// route is always configured for Invoke routing.</param>
    /// <returns>The current instance of <see cref=" CommandRouteBuilderBase{TBuilder}"/> with Invoke routing enabled.</returns>
    public override TBuilder AsInvoke(bool isInvoke = true)
    {
        return (TBuilder)this;
    }

    protected override void PreBuild()
    {
        if (_route.Handler == null)
        {
            throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteBuilderMissingProperty, null, typeof(TBuilder).Name, "Handler");
        }

        RouteSelector selector;

        if (_commandMatch == null && _route.Selector == null)
        {
            // Matching any command if no command match or selector is defined
            selector = CreateSelector((_) => true, InvokeName, PreviewAction);
        }
        else
        {
            selector = CreateSelector(_commandMatch, InvokeName, PreviewAction);
        }

        _route.ChannelId ??= Channels.Msteams;

        if (_route.Selector != null)
        {
            // if the user specified a custom selector we need to make sure it's a valid activity in the handler.
            var existingHandler = _route.Handler;
            _route.Handler = (ctx, ts, ct) =>
            {
                if (!string.Equals(ctx.Activity.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(ctx.Activity.Name, InvokeName)
                    || ctx.Activity.Value == null)
                {
                    throw new InvalidOperationException($"Unexpected {typeof(TBuilder).Name} triggered for activity type: {ctx.Activity.Type}, name: {ctx.Activity.Name}");
                }

                return existingHandler(ctx, ts, ct);
            };

            if (_commandMatch != null)
            {
                var existingSelector = _route.Selector;
                _route.Selector = async (context, ct) =>
                    await selector(context, ct)
                    && await existingSelector(context, ct);
            }
            return;
        }

        _route.Selector = selector;
    }

    private RouteSelector CreateSelector(Func<string, bool> isMatch, string invokeName, string? previewAction = default)
    {
        Task<bool> routeSelector(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            if (!IsContextMatch(turnContext, _route)
                || !turnContext.Activity.IsType(ActivityTypes.Invoke)
                || !string.Equals(turnContext.Activity.Name, invokeName)
                || turnContext.Activity.Value == null)
            {
                return Task.FromResult(false);
            }

            var obj = ProtocolJsonSerializer.ToJsonElements(turnContext.Activity.Value);

            bool isCommandMatch = obj.TryGetValue("commandId", out JsonElement commandId) && commandId.ValueKind == JsonValueKind.String && isMatch(commandId.ToString());

            bool isPreviewActionMatch = previewAction == null || !obj.TryGetValue("botMessagePreviewAction", out JsonElement previewActionToken)
                || string.IsNullOrEmpty(previewActionToken.ToString())
                || string.Equals(previewAction, previewActionToken.ToString());

            return Task.FromResult(isCommandMatch && isPreviewActionMatch);
        }
        return routeSelector;
    }
}
