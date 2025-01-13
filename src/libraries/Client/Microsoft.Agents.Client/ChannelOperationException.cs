// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Client
{
    public class ChannelOperationException : Exception
    {
        public ChannelOperationException(string message) : base(message)
        {
        }
    }
}
