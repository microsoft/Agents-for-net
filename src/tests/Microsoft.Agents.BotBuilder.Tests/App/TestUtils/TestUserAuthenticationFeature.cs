
using Microsoft.Agents.BotBuilder.App;
using Microsoft.Agents.BotBuilder.App.UserAuth;

namespace Microsoft.Agents.BotBuilder.Tests.App.TestUtils
{
    internal sealed class TestUserAuthenticationFeature : UserAuthenticationFeature
    {
        public TestUserAuthenticationFeature(Application app, UserAuthenticationOptions options) : base(app, options)
        {
        }
    }
}
