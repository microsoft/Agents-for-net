---
title: Microsoft 365 Agents SDK for .NET reference
description: Reference documentation for the Microsoft 365 Agents SDK for .NET.
---

# Microsoft 365 Agents SDK for .NET reference

The Microsoft 365 Agents SDK provides the building blocks for creating multichannel conversational
agents for Microsoft 365, Teams, Copilot Studio, Web Chat, and other platforms.

## Get started

- [Microsoft 365 Agents SDK documentation](https://learn.microsoft.com/microsoft-365/agents-sdk/)
- [SDK source repository](https://github.com/microsoft/Agents-for-net)
- [Samples](https://github.com/microsoft/Agents-for-net/tree/main/src/samples)
- [NuGet packages](https://www.nuget.org/packages?q=Microsoft.Agents)

## Core packages

| Package | Purpose |
| --- | --- |
| `Microsoft.Agents.Core` | Activity Protocol models and core interfaces. |
| `Microsoft.Agents.Builder` | Agent application, routing, middleware, and turn processing. |
| `Microsoft.Agents.Hosting.AspNetCore` | ASP.NET Core hosting and endpoint integration. |
| `Microsoft.Agents.Authentication.Msal` | Microsoft identity authentication using MSAL. |
| `Microsoft.Agents.Storage` | Agent state storage abstractions and implementations. |
| `Microsoft.Agents.Extensions.MSTeams` | Microsoft Teams capabilities and routing extensions. |

## Requirements

New applications should target .NET 8.0 or later.
