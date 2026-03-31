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
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.App.TaskModules;

/// <summary>
/// Provides a base builder for configuring message extension routes for TaskModules that handle verb-based Invoke activities 
/// in an AgentApplication. This builder allows for defining command matching logic using either exact string matches or regular expression 
/// patterns, enabling flexible routing based on the command specified in the incoming activity. The builder ensures that the route is 
/// properly configured for Invoke routing and validates required properties before building the route.
/// </summary>
/// <typeparam name="TBuilder">The type of the builder that extends the functionality of the VerbRouteBuilderBase, enabling fluent
/// configuration.</typeparam>
public class VerbRouteBuilderBase<TBuilder> : RouteBuilderBase<TBuilder> where TBuilder : VerbRouteBuilderBase<TBuilder>
{
    private static readonly string DEFAULT_TASK_DATA_FILTER = "verb";
    private Func<string, bool> _verbMatch;
    private string _verbDataFilter = DEFAULT_TASK_DATA_FILTER;

    protected string InvokeName { get; set; }

    public VerbRouteBuilderBase() : base()
    {
        _route.Flags |= RouteFlags.Invoke;
    }

    /// <summary>
    /// Match a specific verb name.
    /// </summary>
    /// <param name="verb">The verb string to be matched. This parameter cannot be null or whitespace.</param>
    /// <returns>The current instance of the builder, allowing for method chaining.</returns>
    /// <exception cref="InvalidOperationException">Command has already been defined for this builder
    /// instance.</exception>
    public TBuilder WithVerb(string verb)
    {
        AssertionHelpers.ThrowIfNullOrWhiteSpace(verb, nameof(verb));

        if (_verbMatch != null)
        {
            throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"{typeof(TBuilder).Name}.WithVerb({verb})");
        }

        _verbMatch = (input) => string.Equals(verb, input);
        return (TBuilder)this;
    }

    /// <summary>
    /// Match a specific verb name pattern.
    /// </summary>
    /// <param name="verbPattern">The verb Regex pattern to be matched. This parameter cannot be null.</param>
    /// <returns>The current instance of the builder, allowing for method chaining.</returns>
    /// <exception cref="InvalidOperationException">Verb has already been defined for this builder
    /// instance.</exception>
    public TBuilder WithVerb(Regex verbPattern)
    {
        AssertionHelpers.ThrowIfNull(verbPattern, nameof(verbPattern));
        if (_verbMatch != null)
        {
            throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"{typeof(TBuilder).Name}.WithVerb(Regex({verbPattern}))");
        }

        _verbMatch = (string input) => verbPattern.IsMatch(input);
        return (TBuilder)this;
    }

    public TBuilder WithFilter(string verbDataFilter)
    {
        _verbDataFilter = string.IsNullOrWhiteSpace(verbDataFilter) ? DEFAULT_TASK_DATA_FILTER : verbDataFilter?.Trim();
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
    /// <returns>The current instance of <see cref="VerbRouteBuilderBase{TBuilder}"/> with Invoke routing enabled.</returns>
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

        if (_verbMatch == null && _route.Selector == null)
        {
            throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteBuilderMissingProperty, null, typeof(TBuilder).Name, "Verb or Selector");
        }

        _route.ChannelId ??= Channels.Msteams;
        _verbDataFilter ??= DEFAULT_TASK_DATA_FILTER;

        var selector = CreateSelector(_verbMatch, _verbDataFilter, InvokeName);

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

            if (_verbMatch != null)
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

    private RouteSelector CreateSelector(Func<string, bool> isMatch, string filter, string invokeName)
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
            if (!obj.TryGetValue("data", out var dataElement))
            {
                return Task.FromResult(false);
            }

            var data = JsonObject.Create(dataElement);
            if (data == null)
            {
                return Task.FromResult(false);
            }

            bool isVerbMatch = data.TryGetPropertyValue(filter, out JsonNode filterField) && filterField.GetValueKind() == JsonValueKind.String
                && isMatch(filterField.ToString());

            return Task.FromResult(isVerbMatch);
        }
        return routeSelector;
    }
}
