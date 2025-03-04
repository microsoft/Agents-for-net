﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Reflection;

namespace Microsoft.Agents.BotBuilder.App
{
    internal interface IRouteAttribute
    {
        void AddRoute(AgentApplication app, MethodInfo method);
    }
}
