using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App
{
    public partial class AgentApplication
    {
        public AgentApplication OnMessage(string text, RouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            return AddRoute(MessageRouteBuilder.Create()
                .WithText(text)
                .WithHandler(handler)
                .WithOrderRank(rank)
                .WithOAuthHandlers(autoSignInHandlers)
                .AsAgentic(isAgenticOnly)
                .Build()
            );
        }
    }
}
