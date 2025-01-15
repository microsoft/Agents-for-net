
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;

namespace Microsoft.Agents.BotBuilder.TestBot.Shared.Debugging
{
    public class DebugAdapter : CloudAdapter
    {
        public DebugAdapter(IChannelServiceClientFactory channelServiceClientFactory, IActivityTaskQueue activityTaskQueue) : base(channelServiceClientFactory, activityTaskQueue, async: false)
        {
        }
    }
}
