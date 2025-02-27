// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.BotBuilder;

namespace Microsoft.Agents.Samples.Bots
{
    /// <summary>
    /// ASP.Net Controller that receives incoming HTTP requests from the Azure Bot Service or other
    /// configured event / activity protocol sources. When called, the request has already been 
    /// authorized and credentials and tokens validated.
    /// </summary>
    /// <param name="adapter"></param>
    /// <param name="bot"></param>
    [Authorize]
    [ApiController]
    [Route("api/messages")]
    public class BotController(IBotHttpAdapter adapter, IBot bot) : ControllerBase
    {
        [HttpPost]
        public async Task PostAsync(CancellationToken cancellationToken)
        {
            // Delegate the processing of the HTTP POST to the adapter.
            // The adapter will invoke the bot.                                   
            await adapter.ProcessAsync(Request, Response, bot, cancellationToken);
        }
    }
}
