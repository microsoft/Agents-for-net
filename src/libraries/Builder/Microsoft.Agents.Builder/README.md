# Microsoft.Agents.Builder

## About

The primary package for building agents with the Microsoft 365 Agents SDK. Provides the `AgentApplication` routing framework, middleware pipeline, and turn context model for handling conversational activities across channels and platforms.

## Main Types

- `AgentApplication`: Base class for agents with route-based activity handling and middleware support
- `IAgent`: Core agent interface implemented by all agents
- `ITurnContext`: Provides access to the current activity, channel, and services for a given turn
- `ITurnState`: Per-turn state container
