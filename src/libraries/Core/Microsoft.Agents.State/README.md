# Microsoft.Agents.State

## About

A bot is inherently stateless. Once your bot is deployed, it may not run in the same process or on the same machine from one turn to the next. However, your bot may need to track the context of a conversation so that it can manage its behavior and remember answers to previous questions. The state and storage features of the Agents SDK allow you to add state to your bot. Bots use state management and storage objects to manage and persist state. The state manager provides an abstraction layer that lets you access state properties using property accessors, independent of the type of underlying storage.
