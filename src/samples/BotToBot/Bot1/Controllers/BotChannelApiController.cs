// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Agents.Client;
using Microsoft.Agents.Hosting.AspNetCore;

namespace Bot1.Controllers
{
    // A controller that handles channel replies when Activity.DeliveryMode is `normal`.
    // In this case, the replies are posted to Http endpoints created by ChannelServiceController.
    // Handling of the replies (received Activities) are handled by a IChannelResponseHandler registered in DI.
    [Authorize]
    [ApiController]
    [Route("api/channel")]
    public class BotChannelApiController(IChannelApiHandler handler) : ChannelApiController(handler)
    {
    }
}
