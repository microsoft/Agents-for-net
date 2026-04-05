// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.Errors;
using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.TaskModules;

/// <summary>
/// Provides a base builder for configuring message extension routes for TaskModules that handle key-based Invoke activities
/// in an AgentApplication. This builder allows for defining command matching logic using either exact string matches or regular expression
/// patterns, enabling flexible routing based on the command specified in the incoming activity. The builder ensures that the route is
/// properly configured for Invoke routing and validates required properties before building the route.
/// </summary>
/// <typeparam name="TBuilder">The type of the builder that extends the functionality of the KeyValueRouteBuilderBase, enabling fluent
/// configuration.</typeparam>
public class KeyValueRouteBuilderBase<TBuilder> : RouteBuilderBase<TBuilder> where TBuilder : KeyValueRouteBuilderBase<TBuilder>
{
    private static readonly string DEFAULT_TASK_DATA_KEY = "task";
    private Func<string, bool> _keyMatch;
    private string _keyPropertyName = DEFAULT_TASK_DATA_KEY;

    protected string InvokeName { get; set; }

    public KeyValueRouteBuilderBase() : base()
    {
        _route.Flags |= RouteFlags.Invoke;
    }

    /// <summary>
    /// Match a specific task data value.
    /// </summary>
    /// <remarks>The default key name is "task" unless changed with <see cref="WithKey(string)"/></remarks>
    /// <param name="value">The key value to be matched.</param>
    /// <returns>The current instance of the builder, allowing for method chaining.</returns>
    /// <exception cref="InvalidOperationException">Command has already been defined for this builder
    /// instance.</exception>
    public TBuilder WithValue(string value)
    {
        if (_keyMatch != null)
        {
            throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"{typeof(TBuilder).Name}.WithKeyValue({value})");
        }

        _keyMatch = !string.IsNullOrWhiteSpace(value) ? (input) => string.Equals(value, input) : null;
        return (TBuilder)this;
    }

    /// <summary>
    /// Match a specific task data value pattern.
    /// </summary>
    /// <remarks>The default key name is "task" unless changed with <see cref="WithKey(string)"/></remarks>
    /// <param name="valuePattern">The key value matching the Regex pattern.</param>
    /// <returns>The current instance of the builder, allowing for method chaining.</returns>
    /// <exception cref="InvalidOperationException">Key has already been defined for this builder
    /// instance.</exception>
    public TBuilder WithValue(Regex valuePattern)
    {
        if (_keyMatch != null)
        {
            throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"{typeof(TBuilder).Name}.WithValue(Regex({valuePattern}))");
        }

        _keyMatch = valuePattern != null ? (string input) => valuePattern.IsMatch(input) : null;
        return (TBuilder)this;
    }

    /// <summary>   
    /// Sets the name of the key property in the incoming activity's value payload that will be used for matching the route. By default, this should be set to "task" in the submitted data.
    /// </summary>
    /// <param name="keyName">A filter string that specifies the key data to include. If the value is null or consists only of white space, a
    /// default filter is applied.</param>
    /// <returns>The current instance of the builder, enabling method chaining.</returns>
    public TBuilder WithKey(string keyName)
    {
        _keyPropertyName = string.IsNullOrWhiteSpace(keyName) ? DEFAULT_TASK_DATA_KEY : keyName?.Trim();
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
    /// <returns>The current instance of <see cref="KeyValueRouteBuilderBase{TBuilder}"/> with Invoke routing enabled.</returns>
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

        if (_keyMatch == null && _route.Selector == null)
        {
            // no key value specified — match any valid invoke activity of the right name
            selector = CreateAnyInvokeSelector(InvokeName);
        }
        else
        {
            selector = CreateSelector(_keyMatch, _keyPropertyName, InvokeName);
        }

        _route.ChannelId ??= Channels.Msteams;
        _keyPropertyName ??= DEFAULT_TASK_DATA_KEY;

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

            if (_keyMatch != null)
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

    private RouteSelector CreateAnyInvokeSelector(string invokeName)
    {
        Task<bool> routeSelector(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            return Task.FromResult(
                IsContextMatch(turnContext, _route)
                && turnContext.Activity.IsType(ActivityTypes.Invoke)
                && string.Equals(turnContext.Activity.Name, invokeName)
                && turnContext.Activity.Value != null);
        }
        return routeSelector;
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

            bool isKeyMatch = data.TryGetPropertyValue(filter, out JsonNode filterField) && filterField.GetValueKind() == JsonValueKind.String
                && isMatch(filterField.GetValue<string>());

            return Task.FromResult(isKeyMatch);
        }
        return routeSelector;
    }
}
