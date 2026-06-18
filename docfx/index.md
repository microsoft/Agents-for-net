---
_layout: landing
---

# Microsoft 365 Agents SDK for .NET

Welcome to the API documentation for the **Microsoft 365 Agents SDK for .NET** — a framework for building enterprise-grade conversational agents that work across Microsoft 365, Teams, Copilot Studio, and other platforms.

## Quick Links

- [API Reference](api/)
- [Architecture & Concepts](articles/architecture.md)
- [Getting Started](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/)
- [GitHub Repository](https://github.com/microsoft/Agents-for-net)
- [NuGet Packages](https://www.nuget.org/profiles/Microsoft.Agents)

## SDK Libraries

| Package | Description |
|---------|-------------|
| `Microsoft.Agents.Core` | Core models, serialization, and protocol types |
| `Microsoft.Agents.Builder` | Agent application framework and middleware pipeline |
| `Microsoft.Agents.Builder.Dialogs` | Dialog system for multi-turn conversations |
| `Microsoft.Agents.Builder.Testing` | Test helpers for unit testing agents |
| `Microsoft.Agents.Hosting.AspNetCore` | ASP.NET Core hosting integration |
| `Microsoft.Agents.Client` | Agent-to-agent communication client |
| `Microsoft.Agents.Connector` | Bot Framework Connector client |
| `Microsoft.Agents.CopilotStudio.Client` | Copilot Studio integration client |
| `Microsoft.Agents.Authentication` | Authentication abstractions |
| `Microsoft.Agents.Authentication.Msal` | MSAL-based authentication implementation |
| `Microsoft.Agents.Storage` | State storage abstractions |
| `Microsoft.Agents.Storage.Blobs` | Azure Blob Storage provider |
| `Microsoft.Agents.Storage.CosmosDb` | Azure Cosmos DB storage provider |

## Minimal Example

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddAgent<MyAgent>();
builder.Services.AddSingleton<IStorage, MemoryStorage>();
builder.Services.AddAgentAspNetAuthentication(builder.Configuration);

var app = builder.Build();
app.MapAgentApplicationEndpoints();
app.Run();

public class MyAgent : AgentApplication
{
    public MyAgent(AgentApplicationOptions options) : base(options)
    {
        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);
    }

    private async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        await turnContext.SendActivityAsync($"You said: {turnContext.Activity.Text}", cancellationToken: cancellationToken);
    }
}
```
