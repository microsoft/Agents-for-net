# Microsoft.Agents.Extensions.Teams.AI

## About

An Agents SDK implementation of the Teams AI library for building AI-powered agents in Microsoft Teams. Adds AI orchestration, prompt management, and LLM-based action planning on top of the core Agents SDK.

Issues, releases, and source code: [teams-agents repository](https://github.com/microsoft/teams-agents)

## Main Types

- `TeamsApplication`: Application class for building AI-powered Teams agents
- `TeamsApplicationBuilder`: Builder for configuring a `TeamsApplication`
- `ActionPlanner`: LLM-based planner that generates and executes parameterized action plans
- `PromptManager`: Manages prompt templates, functions, and data sources
