// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.BotBuilder;

namespace Bot1
{
    // This Controller exposes the HTTP endpoints for the Connector that Bot2 hits to
    // send replies.
    [Authorize]
    [ApiController]
    [Route("api/botresponse")]
    public class BotResponseController(IChannelApiHandler handler) : ChannelApiController(handler)
    {
    }
}
