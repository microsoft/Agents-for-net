// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.A2A.Routing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Microsoft.Agents.Extensions.A2A;

/// <summary>
/// Provides A2A skill and message-route registration for an <see cref="AgentApplication"/>.
/// </summary>
public class A2AAgentExtension : Builder.AgentExtension
{
    private readonly AgentApplication _agentApplication;
    private readonly List<A2ASkillRegistration> _skillRegistrations = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="A2AAgentExtension"/> class.
    /// </summary>
    /// <param name="agentApplication">The agent application to configure for the A2A channel.</param>
    public A2AAgentExtension(AgentApplication agentApplication)
    {
        _agentApplication = agentApplication;
        ChannelId = Channels.A2A;
        DiscoverSkillAttributes();
    }

    /// <summary>
    /// Gets the normalized A2A skill registrations configured for this agent.
    /// </summary>
    internal IReadOnlyList<A2ASkillRegistration> SkillRegistrations => _skillRegistrations;

    /// <summary>
    /// Registers an A2A skill and the message route that implements it.
    /// </summary>
    /// <param name="id">The unique skill identifier.</param>
    /// <param name="configure">Configures the skill metadata and message route.</param>
    /// <returns>The current extension instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <see langword="null"/>.</exception>
    public A2AAgentExtension Skill(string id, Action<A2ASkillBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new A2ASkillBuilder(id);
        configure(builder);
        RegisterSkill(builder.Build());
        return this;
    }

    /// <summary>
    /// Registers a message route handler for any A2A message received by the agent.
    /// </summary>
    /// <param name="routeHandler">The delegate that processes incoming A2A message activities. This handler will be invoked when a message
    /// activity is received on the A2A channel.</param>
    /// <param name="autoSigninHandlers">An optional array of handler names that support automatic sign-in. If specified, these handlers will be used to
    /// facilitate OAuth flows for the route.</param>
    /// <param name="rank">The order rank that determines the priority of the route. Use RouteRank.Unspecified to assign the default rank.</param>
    /// <returns>The current instance of A2AAgentExtension to allow method chaining.</returns>
    public A2AAgentExtension OnMessage(A2ARouteHandler routeHandler, string[] autoSigninHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        _agentApplication.AddRoute(TypeRouteBuilder.Create()
            .WithType(ActivityTypes.Message)
            .WithChannelId(ChannelId)
            .WithHandler(HandlerUtils.WrapHandler(routeHandler))
            .WithOrderRank(rank == RouteRank.Unspecified ? RouteRank.Last : rank)
            .WithOAuthHandlers(autoSigninHandlers)
            .Build());
        return this;
    }

    /// <summary>
    /// Registers a message route that triggers the specified handler when an incoming A2A message matches the given
    /// text.
    /// </summary>
    /// <remarks>This differs from AgentApplication.OnMessage in that this only matches for the A2A channel.</remarks>
    /// <param name="text">The text pattern to match incoming A2A messages. The route is triggered when a message matches this text.</param>
    /// <param name="routeHandler">The handler to invoke when the route is matched. Responsible for processing the incoming message.</param>
    /// <param name="autoSigninHandlers">An optional array of OAuth handler names to use for automatic sign-in. If null, no auto sign-in handlers are
    /// applied.</param>
    /// <param name="rank">The rank that determines the order in which this route is evaluated. Use RouteRank.Unspecified for default
    /// ordering.</param>
    /// <returns>The current instance of A2AAgentExtension to allow method chaining.</returns>
    public A2AAgentExtension OnMessage(string text, A2ARouteHandler routeHandler, string[] autoSigninHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        _agentApplication.AddRoute(MessageRouteBuilder.Create()
            .WithText(text)
            .WithChannelId(ChannelId)
            .WithHandler(HandlerUtils.WrapHandler(routeHandler))
            .WithOrderRank(rank)
            .WithOAuthHandlers(autoSigninHandlers)
            .Build());
        return this;
    }

    /// <summary>
    /// Registers a message route that triggers the specified handler when an incoming A2A message matches the given
    /// text pattern.
    /// </summary>
    /// <remarks>This differs from AgentApplication.OnMessage in that this only matches for the A2A channel.</remarks>
    /// <param name="textPattern">A regular expression used to match the text of incoming A2A messages. The route is triggered when the message
    /// text matches this pattern.</param>
    /// <param name="routeHandler">The handler to invoke when the route is matched. This delegate processes the incoming message.</param>
    /// <param name="autoSigninHandlers">An optional array of OAuth handler names to use for automatic sign-in if authentication is required. May be null
    /// if no auto sign-in is needed.</param>
    /// <param name="rank">The rank that determines the order in which this route is evaluated relative to other routes. Lower values
    /// indicate higher priority. The default is RouteRank.Unspecified.</param>
    /// <returns>The current instance of A2AAgentExtension to allow method chaining.</returns>
    public A2AAgentExtension OnMessage(Regex textPattern, A2ARouteHandler routeHandler, string[] autoSigninHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        _agentApplication.AddRoute(MessageRouteBuilder.Create()
            .WithText(textPattern)
            .WithChannelId(ChannelId)
            .WithHandler(HandlerUtils.WrapHandler(routeHandler))
            .WithOrderRank(rank)
            .WithOAuthHandlers(autoSigninHandlers)
            .Build());
        return this;
    }

    private void DiscoverSkillAttributes()
    {
        var attributedSkills = _agentApplication.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .SelectMany(method => method
                .GetCustomAttributes<A2ASkillAttribute>(inherit: true)
                .Select(attribute => (Method: method, Attribute: attribute)))
            .ToList();

        foreach (var group in attributedSkills.GroupBy(skill => skill.Attribute.Id, StringComparer.Ordinal))
        {
            var first = group.First().Attribute;
            if (group.Any(skill => !HasCompatibleMetadata(first, skill.Attribute)))
            {
                throw new InvalidOperationException($"A2A skill '{group.Key}' has conflicting metadata.");
            }

            foreach (var skill in group)
            {
                RegisterSkill(skill.Attribute.CreateBuilder(skill.Method, _agentApplication).Build());
            }
        }
    }

    private void RegisterSkill(A2ASkillRegistration registration)
    {
        _skillRegistrations.Add(registration);
        AddRoute(
            _agentApplication,
            registration.RouteSelector,
            HandlerUtils.WrapHandler(registration.Handler),
            isAgenticOnly: registration.IsAgenticOnly,
            rank: registration.Rank,
            autoSignInHandlers: registration.AutoSignInHandlers.ToArray());
    }

    private static bool HasCompatibleMetadata(A2ASkillAttribute left, A2ASkillAttribute right)
    {
        return string.Equals(left.Name, right.Name, StringComparison.Ordinal)
            && string.Equals(left.Description, right.Description, StringComparison.Ordinal)
            && left.Tags.SequenceEqual(right.Tags, StringComparer.Ordinal)
            && (left.Examples ?? []).SequenceEqual(right.Examples ?? [], StringComparer.Ordinal)
            && (left.InputModes ?? []).SequenceEqual(right.InputModes ?? [], StringComparer.Ordinal)
            && (left.OutputModes ?? []).SequenceEqual(right.OutputModes ?? [], StringComparer.Ordinal)
            && left.AutoSignInHandlers.SequenceEqual(right.AutoSignInHandlers, StringComparer.Ordinal);
    }
}