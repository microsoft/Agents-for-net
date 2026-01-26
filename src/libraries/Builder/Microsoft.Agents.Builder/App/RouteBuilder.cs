
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using System;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App
{
    public class RouteBuilder
    {
        private readonly Route _route = new();

        internal RouteBuilder() { }

        public static RouteBuilder Create(bool isInvoke = false, bool isAgentic = false)
        {
            var builder = new RouteBuilder();
            builder.AsInvoke(isInvoke);
            builder.AsAgentic(isAgentic);
            return builder;
        }

        public RouteBuilder WithSelector(RouteSelector selector)
        {
            _route.Selector = selector;
            return this;
        }

        public RouteBuilder WithMessage(string text)
        {
            _route.Selector = (context, ct) => Task.FromResult
                (
                    (!_route.Flags.HasFlag(RouteFlags.Agentic) || AgenticAuthorization.IsAgenticRequest(context))
                    && context.Activity.IsType(ActivityTypes.Message)
                    && context.Activity?.Text != null
                    && context.Activity.Text.Equals(text, StringComparison.OrdinalIgnoreCase)
                );
            
            return this;
        }

        public RouteBuilder WithHander(RouteHandler handler)
        {
            _route.Handler = handler;
            return this;
        }

        public RouteBuilder WithOAuthHandlers(string[] handlers)
        {
            _route.OAuthHandlers = context => handlers ?? [];
            return this;
        }

        public RouteBuilder WithOAuthHandlers(Func<ITurnContext, string[]> handlers)
        {
            _route.OAuthHandlers = handlers ?? (Func<ITurnContext, string[]>) (context => []);
            return this;
        }

        private RouteBuilder AsInvoke(bool isInvoke)
        {
            if (isInvoke)
            {
                _route.Flags |= RouteFlags.Invoke;
            }
            else
            {
                _route.Flags &= ~RouteFlags.Invoke;
            }
            return this;
        }

        private RouteBuilder AsAgentic(bool isAgentic)
        {
            if (isAgentic)
            {
                _route.Flags |= RouteFlags.Agentic;
            }
            else
            {
                _route.Flags &= ~RouteFlags.Agentic;
            }
            return this;
        }

        public RouteBuilder AsNonTerminal()
        {
            _route.Flags |= RouteFlags.NonTerminal;
            return this;
        }

        public RouteBuilder WithOrderRank(ushort rank)
        {
            _route.Rank = rank;
            return this;
        }

        public Route Build()
        {
            AssertionHelpers.ThrowIfNull(_route.Selector, nameof(_route.Selector));
            AssertionHelpers.ThrowIfNull(_route.Handler, nameof(_route.Handler));

            return _route; 
        }
    }
}
