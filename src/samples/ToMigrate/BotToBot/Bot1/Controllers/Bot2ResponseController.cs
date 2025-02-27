// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.BotBuilder;

namespace Microsoft.Agents.Samples.Bots
{
    // A controller that handles channel replies to the bot.
    [Authorize]
    [ApiController]
    [Route("api/botresponse")]
    public class Bot2ResponseController(IChannelApiHandler handler) : ChannelApiController(handler)
    {
    }
}
