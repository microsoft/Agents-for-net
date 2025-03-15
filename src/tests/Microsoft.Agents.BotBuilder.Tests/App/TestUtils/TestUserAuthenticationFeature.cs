
using Microsoft.Agents.BotBuilder.App;
using Microsoft.Agents.BotBuilder.App.UserAuth;

namespace Microsoft.Agents.BotBuilder.Tests.App.TestUtils
{
    internal sealed class TestUserAuthenticationFeature : UserAuthorizationFeature
    {
        public TestUserAuthenticationFeature(AgentApplication app, UserAuthorizationOptions options) : base(app, options)
        {
        }
    }
}
