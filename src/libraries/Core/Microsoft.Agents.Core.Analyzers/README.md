# Microsoft.Agents.Core.Analyzers

## About

Provides Roslyn analyzers and code fixes for the Microsoft 365 Agents SDK. Helps detect common SDK usage errors and enforce conventions at compile time, improving developer productivity and correctness.

## Rules

| ID | Severity | Description |
|----|----------|-------------|
| MAA001 | Error | A class decorated with an `AgentExtension` attribute must be declared `partial`. |
| MAA002 | Error | A method decorated with a route attribute (any `IRouteAttribute`) must match the handler delegate signature the attribute declares via `[RouteHandlerType(typeof(...))]`. A code fix is offered to rewrite the method signature. |

## Suppressors

| Suppressed ID | Description |
|---------------|-------------|
| IDE0051 | Private methods decorated with a route attribute (any `IRouteAttribute`) are wired up declaratively and invoked via route registration, so the "unused private member" diagnostic is suppressed for them. Genuinely unused private members are still reported. |
