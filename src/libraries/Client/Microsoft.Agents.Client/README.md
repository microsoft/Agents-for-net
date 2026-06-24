# Microsoft.Agents.Client

## About

Enables agent-to-agent communication using the Activity Protocol. An agent uses this package to start and manage conversations with other agents, supporting both synchronous and streaming response delivery modes.

## Main Types

- `IAgentHost`: Manages conversations with other agents
- `AddAgentHost()`: DI extension to register agent-to-agent capabilities
- `AgentResponses`: Extension for receiving replies from other agents within an `AgentApplication`
