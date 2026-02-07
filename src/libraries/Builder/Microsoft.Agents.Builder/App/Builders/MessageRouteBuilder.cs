// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App.Builders
{
    /// <summary>
    /// RouteBuilder for routing Message activities in an AgentApplication.
    /// </summary>
    public class MessageRouteBuilder : RouteBuilderBase<MessageRouteBuilder>
    {
        /// <summary>
        /// Adds a selector to the route that matches incoming message activities with text equal to the specified
        /// value, ignoring case.
        /// </summary>
        /// <remarks>This method only matches activities of type 'Message' and will not match other
        /// activity types. If the route is marked as agentic, the selector will only match agentic requests as
        /// determined by AgenticAuthorization. Use this method to restrict route handling to specific message text
        /// values.</remarks>
        /// <param name="text">The text to match against the incoming activity's message content. Comparison is case-insensitive. Cannot be
        /// null.</param>
        /// <returns>A RouteBuilder instance with the added selector for matching message text.</returns>
        public MessageRouteBuilder WithText(string text)
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

        /// <summary>
        /// Adds a text pattern selector to the route, matching incoming message activities whose text satisfies the
        /// specified regular expression.
        /// </summary>
        /// <remarks>This method only applies the selector to message activities. If the route is marked
        /// as agentic, the selector will only match agentic requests. Use this method to restrict route handling to
        /// messages whose text matches a specific pattern.</remarks>
        /// <param name="textPattern">The regular expression used to match the text of incoming message activities. Cannot be null. The selector
        /// will only match activities whose text property is not null and matches this pattern.</param>
        /// <returns>A RouteBuilder instance configured with the specified text pattern selector.</returns>
        public MessageRouteBuilder WithText(Regex textPattern)
        {
            _route.Selector = (context, ct) => Task.FromResult
                (
                    (!_route.Flags.HasFlag(RouteFlags.Agentic) || AgenticAuthorization.IsAgenticRequest(context))
                    && context.Activity.IsType(ActivityTypes.Message)
                    && context.Activity?.Text != null
                    && textPattern.IsMatch(context.Activity.Text)
                );
            return this;
        }

        /// <summary>
        /// Returns the current message route builder instance. For message routes, the invoke flag is ignored to
        /// prevent misconfiguration.
        /// </summary>
        /// <remarks>Messages cannot be configured as invoke routes. This method always returns the
        /// current instance, regardless of the value of <paramref name="isInvoke"/>.</remarks>
        /// <param name="isInvoke">Ignored</param>
        /// <returns>The current instance of <see cref="MessageRouteBuilder"/>.</returns>
        public new MessageRouteBuilder AsInvoke(bool isInvoke = true)
        {
            return this;
        }
    }
}
