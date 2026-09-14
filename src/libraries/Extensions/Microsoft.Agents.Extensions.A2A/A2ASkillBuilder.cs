// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Microsoft.Agents.Extensions.A2A;

/// <summary>
/// Configures an A2A skill and the message route that implements it.
/// </summary>
public sealed class A2ASkillBuilder
{
    private readonly string _id;
    private string _name;
    private string _description;
    private IEnumerable<string> _tags = [];
    private IEnumerable<string> _examples = [];
    private IEnumerable<string> _inputModes = [];
    private IEnumerable<string> _outputModes = [];
    private RouteSelector _routeSelector;
    private A2ARouteHandler _handler;
    private ushort _rank;
    private bool _isAgenticOnly;
    private IEnumerable<string> _autoSignInHandlers = [];

    internal A2ASkillBuilder(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        _id = id;
        _name = id;
        _description = id;
    }

    /// <summary>
    /// Sets the human-readable skill name.
    /// </summary>
    public A2ASkillBuilder WithName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _name = name;
        return this;
    }

    /// <summary>
    /// Sets the skill description.
    /// </summary>
    public A2ASkillBuilder WithDescription(string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        _description = description;
        return this;
    }

    /// <summary>
    /// Sets the skill tags.
    /// </summary>
    public A2ASkillBuilder WithTags(params string[] tags)
    {
        _tags = tags ?? [];
        return this;
    }

    /// <summary>
    /// Sets example skill requests.
    /// </summary>
    public A2ASkillBuilder WithExamples(params string[] examples)
    {
        _examples = examples ?? [];
        return this;
    }

    /// <summary>
    /// Sets supported input modes.
    /// </summary>
    public A2ASkillBuilder WithInputModes(params string[] inputModes)
    {
        _inputModes = inputModes ?? [];
        return this;
    }

    /// <summary>
    /// Sets supported output modes.
    /// </summary>
    public A2ASkillBuilder WithOutputModes(params string[] outputModes)
    {
        _outputModes = outputModes ?? [];
        return this;
    }

    /// <summary>
    /// Configures a route for all A2A message activities.
    /// </summary>
    public A2ASkillBuilder OnMessage(A2ARouteHandler handler, string[] autoSigninHandlers = null, ushort rank = RouteRank.Unspecified, bool isAgenticOnly = false)
    {
        return SetRoute(
            MessageRouteBuilder.Create(),
            handler,
            autoSigninHandlers,
            rank,
            isAgenticOnly);
    }

    /// <summary>
    /// Configures a route for A2A message activities with matching text.
    /// </summary>
    public A2ASkillBuilder OnMessage(string text, A2ARouteHandler handler, string[] autoSigninHandlers = null, ushort rank = RouteRank.Unspecified, bool isAgenticOnly = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        return SetRoute(
            MessageRouteBuilder.Create().WithText(text),
            handler,
            autoSigninHandlers,
            rank,
            isAgenticOnly);
    }

    /// <summary>
    /// Configures a route for A2A message activities with matching text.
    /// </summary>
    public A2ASkillBuilder OnMessage(Regex textPattern, A2ARouteHandler handler, string[] autoSigninHandlers = null, ushort rank = RouteRank.Unspecified, bool isAgenticOnly = false)
    {
        ArgumentNullException.ThrowIfNull(textPattern);
        return SetRoute(
            MessageRouteBuilder.Create().WithText(textPattern),
            handler,
            autoSigninHandlers,
            rank,
            isAgenticOnly);
    }

    internal A2ASkillRegistration Build()
    {
        if (_routeSelector == null || _handler == null)
        {
            throw new InvalidOperationException("An A2A skill must define a message route.");
        }

        return new A2ASkillRegistration(
            _id,
            _name,
            _description,
            _tags,
            _examples,
            _inputModes,
            _outputModes,
            _routeSelector,
            _handler,
            _rank,
            _isAgenticOnly,
            _autoSignInHandlers);
    }

    private A2ASkillBuilder SetRoute(
        MessageRouteBuilder builder,
        A2ARouteHandler handler,
        string[] autoSigninHandlers,
        ushort rank,
        bool isAgenticOnly)
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (_routeSelector != null)
        {
            throw new InvalidOperationException("An A2A skill can define only one message route.");
        }

        var route = builder
            .WithChannelId(Channels.A2A)
            .WithHandler(HandlerUtils.WrapHandler(handler))
            .WithOrderRank(rank)
            .AsAgentic(isAgenticOnly)
            .WithOAuthHandlers(autoSigninHandlers)
            .Build();

        _routeSelector = route.Selector;
        _handler = handler;
        _rank = route.Rank;
        _isAgenticOnly = isAgenticOnly;
        _autoSignInHandlers = autoSigninHandlers ?? [];
        return this;
    }
}
