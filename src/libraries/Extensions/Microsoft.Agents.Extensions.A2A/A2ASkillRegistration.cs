// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Microsoft.Agents.Extensions.A2A;

/// <summary>
/// An immutable A2A skill definition and the route that implements it.
/// </summary>
internal sealed class A2ASkillRegistration
{
    public A2ASkillRegistration(
        string id,
        string name,
        string description,
        IEnumerable<string> tags,
        IEnumerable<string> examples,
        IEnumerable<string> inputModes,
        IEnumerable<string> outputModes,
        RouteSelector routeSelector,
        A2ARouteHandler handler,
        ushort rank,
        bool isAgenticOnly,
        IEnumerable<string> autoSignInHandlers)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(routeSelector);
        ArgumentNullException.ThrowIfNull(handler);

        Id = id;
        Name = name;
        Description = description;
        Tags = ToReadOnlyList(tags);
        Examples = ToReadOnlyList(examples);
        InputModes = ToReadOnlyList(inputModes);
        OutputModes = ToReadOnlyList(outputModes);
        RouteSelector = routeSelector;
        Handler = handler;
        Rank = rank;
        IsAgenticOnly = isAgenticOnly;
        AutoSignInHandlers = ToReadOnlyList(autoSignInHandlers);
    }

    public string Id { get; }

    public string Name { get; }

    public string Description { get; }

    public IReadOnlyList<string> Tags { get; }

    public IReadOnlyList<string> Examples { get; }

    public IReadOnlyList<string> InputModes { get; }

    public IReadOnlyList<string> OutputModes { get; }

    public RouteSelector RouteSelector { get; }

    public A2ARouteHandler Handler { get; }

    public ushort Rank { get; }

    public bool IsAgenticOnly { get; }

    public IReadOnlyList<string> AutoSignInHandlers { get; }

    private static ReadOnlyCollection<string> ToReadOnlyList(IEnumerable<string> values)
    {
        return Array.AsReadOnly(values?.ToArray() ?? []);
    }
}
