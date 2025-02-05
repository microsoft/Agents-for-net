// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Client
{
    public class ChannelOperationException(string message) : Exception(message)
    {
    }
}
