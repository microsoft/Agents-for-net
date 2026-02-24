// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Builder.App.Proactive
{
    /// <summary>
    /// Specifies that a method should participate in continuing an existing conversation flow, using the provided
    /// delegate and optional route segment.
    /// </summary>
    /// <remarks>Apply this attribute to methods that are intended to handle continuation of conversations,
    /// such as in conversational bots or workflow systems. The attribute identifies the delegate responsible for
    /// handling the continuation and optionally specifies a route segment to distinguish between different conversation
    /// paths. This attribute can be inherited by derived classes.</remarks>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class ContinueConversationAttribute : Attribute
    {
        public const string DefaultKey = "";

        public ContinueConversationAttribute(string key = DefaultKey)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
        }

        public string Key { get; }
    }
}
