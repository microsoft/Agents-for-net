// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System.Net;

namespace Microsoft.Agents.BotBuilder.App
{
    public static class ActivityUtilities
    {
        public static Activity CreateInvokeResponseActivity(object? body = default)
        {
            Activity activity = new()
            {
                Type = ActivityTypes.InvokeResponse,
                Value = new InvokeResponse { Status = (int)HttpStatusCode.OK, Body = body }
            };
            return activity;
        }
    }
}
