# Microsoft.Agents.Hosting.AspNetCore

- Required package for a hosting an Agent in AspNet.
- Provides extension methods to add an Agents using DI.

## How-to use

```cs
builder.AddAgent<TAgent>();
```

Or

```cs
builder.AddAgent(sp =>
{
	var options = new AgentApplicationOptions()
	{
	};

	var app = new AgentApplication(options);

	return app;
});
```
