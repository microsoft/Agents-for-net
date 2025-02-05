﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using System.Threading;
using Microsoft.Agents.Core.Interfaces;

namespace TagMentionBot.Controllers
{
    // ASP.Net Controller that receives incoming HTTP requests from the Azure Bot Service or other configured event activity protocol sources.
    // When called, the request has already been authorized and credentials and tokens validated.
    [Authorize]
    [ApiController]
    [Route("api/messages")]
    public class BotController(IBotHttpAdapter adapter, IBot bot) : ControllerBase
    {
        [HttpPost]
        public Task PostAsync(CancellationToken cancellationToken)
            => adapter.ProcessAsync(Request, Response, bot, cancellationToken);

    }
}
