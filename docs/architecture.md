# SDK Architecture

High-level system diagram showing the layered library structure and their relationships.

## Diagram

```mermaid
graph TD
    %% External callers
    Channel["Channel / Client<br/>(Teams, Web Chat, Bot Service)"]
    PipeClient["DirectLineFlex Sidecar"]
    ParentAgent["Parent Agent"]

    %% Hosting layer
    subgraph Hosting ["Hosting"]
        AspNetCore["Hosting.AspNetCore<br/><i>AddAgent, MapEndpoints</i>"]
        NamedPipes["Hosting.DirectLine.NamedPipes<br/><i>AddAgentNamedPipeTransport</i>"]
    end

    %% Builder layer
    subgraph Builder ["Builder"]
        AgentApp["Builder<br/><i>AgentApplication, ITurnContext,<br/>ITurnState, Middleware</i>"]
    end

    %% Client layer
    subgraph Client ["Client"]
        AgentClient["Client<br/><i>IAgentHost, Agent-to-Agent</i>"]
        Connector["Connector<br/><i>Bot Service channels</i>"]
        CopilotStudio["CopilotStudio.Client<br/><i>Copilot Studio agents</i>"]
    end

    %% Extensions layer
    subgraph Extensions ["Extensions"]
        Teams["Extensions.Teams"]
        SharePoint["Extensions.SharePoint"]
    end

    %% Storage layer
    subgraph Storage ["Storage"]
        StorageBase["Storage<br/><i>IStorage, MemoryStorage</i>"]
        Blobs["Storage.Blobs"]
        CosmosDb["Storage.CosmosDb"]
    end

    %% Core layer
    subgraph Core ["Core"]
        CoreLib["Core<br/><i>Activity Protocol models,<br/>ProtocolJsonSerializer, Telemetry</i>"]
    end

    %% Authentication layer
    subgraph Auth ["Authentication"]
        Msal["Authentication.Msal<br/><i>ClientSecret, Federated,<br/>Managed Identity</i>"]
    end

    %% Inbound flows
    Channel --> AspNetCore
    PipeClient --> NamedPipes
    AspNetCore --> AgentApp
    NamedPipes --> AgentApp

    %% Builder dependencies
    AgentApp --> Extensions
    AgentApp --> StorageBase
    AgentApp --> CoreLib

    %% Outbound flows (Agent-to-Agent, channel replies)
    AgentApp --> AgentClient
    AgentApp --> Connector
    AgentApp --> CopilotStudio
    ParentAgent --> AgentClient

    %% Client dependencies
    AgentClient --> CoreLib
    AgentClient --> Msal
    Connector --> CoreLib
    Connector --> Msal
    CopilotStudio --> CoreLib
    CopilotStudio --> Msal

    %% Extensions depend on Core
    Teams --> CoreLib
    SharePoint --> CoreLib

    %% Storage depends on Core
    StorageBase --> CoreLib
    Blobs --> StorageBase
    CosmosDb --> StorageBase

    %% Auth depends on Core
    Msal --> CoreLib

    %% Hosting depends on Builder + Auth
    AspNetCore --> AgentApp
    AspNetCore --> Msal
    NamedPipes --> AgentApp
```

## Layer Summary

| Layer | Purpose |
|-------|---------|
| **Hosting** | Receives inbound HTTP or named-pipe requests and dispatches to the agent |
| **Builder** | Agent construction, route-based handler registration, middleware pipeline |
| **Client** | Outbound communication — Agent-to-Agent, Bot Service channels, Copilot Studio |
| **Extensions** | Platform-specific capabilities (Teams, SharePoint) |
| **Storage** | State persistence (memory, Blob, CosmosDb) |
| **Authentication** | Token acquisition via MSAL (secrets, federated creds, managed identity) |
| **Core** | Activity Protocol models, serialization, telemetry — foundation for all layers |
