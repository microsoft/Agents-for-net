# Microsoft.Agents.BotBuilder

## About

Contains the implementation for the Agents SDK Bot

## How to use with Microsoft.Agents.Hosting.AspNetCore

```cs
 public class EchoBot : ActivityHandler
 {
     protected override Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken) 
        => turnContext.SendActivityAsync(turnContext.Activity.Text, cancellationToken)
 }


[Authorize]    
[ApiController]
[Route("api/messages")]
public class BotController(IBotHttpAdapter adapter, IBot bot) : ControllerBase
{
    [HttpPost]
    public Task PostAsync(CancellationToken cancellationToken) 
        => adapter.ProcessAsync(Request, Response, bot, cancellationToken);            
}
```

## Main Types

The main types provided by this library are:

- `Microsoft.Agents.Core.Models.Activity`
- `Microsoft.Agents.Core.Interfaces.ITurnContext`
- `Microsoft.Agents.BotBuilder.ActivityHandler`
