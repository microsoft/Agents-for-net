// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Client;
using System;

namespace Microsoft.Agents.Hosting.AspNetCore
{
    /// <summary>
    /// This contains the endpoint for Bot replies for DeliverMode == 'normal'.
    /// </summary>
    /// <param name="handler">A <see cref="IChannelResponseHandler"/> that will handle the incoming request.</param>
    // Note: this class is marked as abstract to prevent the ASP runtime from registering it as a controller.
    [ChannelResponseExceptionFilter]
    public abstract class ChannelResponseController(IChannelResponseHandler handler) : ControllerBase
    {
        private readonly IChannelResponseHandler _handler = handler ?? throw new ArgumentNullException(nameof(handler));

        /// <summary>
        /// SendToConversation.
        /// </summary>
        /// <param name="conversationId">Conversation ID.</param>
        /// <param name="activity">Activity to send.</param>
        /// <returns>Task representing result of sending activity to conversation.</returns>
        [HttpPost("v3/conversations/{conversationId}/send")]
        public virtual async Task<IActionResult> SendToActivityAsync(string conversationId)
        {
            var activity = await GetActivityAsync();
            if (activity == null)
            {
                return null;
            }

            var claimsIdentity = User?.Identity as ClaimsIdentity;
            var result = await _handler.OnSendActivityAsync(claimsIdentity, conversationId, activity).ConfigureAwait(false);
            return new JsonResult(result);
        }

        protected async Task<Activity> GetActivityAsync()
        {
            return await HttpHelper.ReadRequestAsync<Activity>(Request).ConfigureAwait(false);
        }
    }
}
