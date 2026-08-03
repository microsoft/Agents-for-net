// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.Errors;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Errors;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;

namespace Microsoft.Agents.Builder.App
{
    /// <summary>
    /// Validates the configuration <see cref="AgentApplicationOptions"/> is created from.
    /// </summary>
    /// <remarks>
    /// <para>DI creates <see cref="AgentApplicationOptions"/> on first use, which for an Agent is the first inbound
    /// Activity.  Configuration mistakes, such as a <c>DefaultHandlerName</c> that doesn't name a defined handler,
    /// are otherwise not reported until a user sends a message.  A host can call this during startup to report them
    /// while the Agent is starting instead.</para>
    /// <para>Only configuration is inspected.  Nothing is instantiated, so this has no side effects and doesn't
    /// require the rest of the Agent to be resolvable.  The exceptions thrown are the same ones the Agent would
    /// throw on the first Activity.</para>
    /// </remarks>
    internal static class AgentApplicationConfigurationValidator
    {
        private const string UserAuthorizationKey = "UserAuthorization";
        private const string HandlersKey = "Handlers";
        private const string DefaultHandlerNameKey = "DefaultHandlerName";

        /// <summary>
        /// Validates the AgentApplication configuration section.
        /// </summary>
        /// <param name="configuration">The configuration <see cref="AgentApplicationOptions"/> is created from.</param>
        /// <param name="configKey">The AgentApplication config section name.</param>
        /// <exception cref="InvalidOperationException">User authorization is configured without handlers.</exception>
        /// <exception cref="IndexOutOfRangeException"><c>DefaultHandlerName</c> doesn't name a defined handler.</exception>
        public static void Validate(IConfiguration configuration, string configKey = "AgentApplication")
        {
            AssertionHelpers.ThrowIfNull(configuration, nameof(configuration));

            var section = configuration.GetSection(configKey);
            if (!section.Exists())
            {
                // This is to compensate for IConfiguration containing the class name as the section name.
                section = configuration.GetSection(nameof(AgentApplicationOptions));
                if (!section.Exists())
                {
                    // Nothing is configured.  The Agent will use AgentApplicationOptions defaults.
                    return;
                }
            }

            ValidateUserAuthorization(section.GetSection(UserAuthorizationKey));
        }

        private static void ValidateUserAuthorization(IConfigurationSection section)
        {
            if (!section.Exists())
            {
                // User authorization is optional.
                return;
            }

            var handlerNames = section.GetSection(HandlersKey).GetChildren().Select(handler => handler.Key).ToList();
            if (handlerNames.Count == 0)
            {
                throw ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.NoUserAuthorizationHandlers, null);
            }

            var defaultHandlerName = section.GetValue<string>(DefaultHandlerNameKey);
            if (string.IsNullOrWhiteSpace(defaultHandlerName))
            {
                // The first handler defined is used when a default isn't specified.
                return;
            }

            // Matches how UserAuthorizationDispatcher looks up a handler name.
            if (!handlerNames.Any(handlerName => string.Equals(handlerName, defaultHandlerName.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                throw ExceptionHelper.GenerateException<IndexOutOfRangeException>(ErrorHelper.UserAuthorizationDefaultHandlerNotFound, null, defaultHandlerName);
            }
        }
    }
}
