# Microsoft.Agents.Builder.Testing

## About

Provides utilities for unit testing agents built with the Microsoft 365 Agents SDK. Supports sending activities to an agent and asserting on its replies, with optional AI-based semantic validation.

## Main Types

- `AgentTestHost`: Host for running an agent in a test environment
- `TestAdapter`: In-process channel adapter for sending and receiving activities in tests
- `TestFlow`: Fluent API for structuring multi-turn conversation tests
- `IResponseValidator`: Interface for reply assertion strategies, including `SemanticValidator` for AI-based assertions
