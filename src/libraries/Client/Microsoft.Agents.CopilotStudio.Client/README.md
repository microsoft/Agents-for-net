# Microsoft.Agents.CopilotStudio.Client

## About

Provides a client for communicating with agents built in Microsoft Copilot Studio. Enables .NET applications to start and manage conversations with Copilot Studio agents via the conversational channel.

Requires the `CopilotStudio.Copilots.Invoke` API permission on an Entra ID app registration. Supports user-based (delegated) and service principal (application) authentication flows.

## Main Types

- `CopilotClient`: Client for interacting with a Copilot Studio agent
- `ConnectionSettings`: Configures the target agent using `EnvironmentId` + `SchemaName`, or a `DirectConnectUrl`

See [Integrate with Copilot Studio](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/integrate-with-mcs) for more information.
