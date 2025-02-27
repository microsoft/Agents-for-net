// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Extensions.Teams.Serialization.Converters;
using System.Text.Json;

namespace Microsoft.Agents.Extensions.Teams.Serialization
{
    internal static class SerializerExtensions
    {
        public static JsonSerializerOptions ApplyTeamsOptions(this JsonSerializerOptions options)
        {
            options.Converters.Add(new SurfaceConverter());
            options.Converters.Add(new TabSubmitDataConverter());
            options.Converters.Add(new TeamsChannelDataConverter());
            options.Converters.Add(new MessagingExtensionActionResponseConverter());
            options.Converters.Add(new TaskModuleResponseConverter());
            options.Converters.Add(new TaskModuleResponseBaseConverter());
            options.Converters.Add(new TaskModuleCardResponseConverter());
            options.Converters.Add(new TaskModuleContinueResponseConverter());
            options.Converters.Add(new TaskModuleMessageResponseConverter());
            options.Converters.Add(new MessagingExtensionAttachmentConverter());
            options.Converters.Add(new TeamsChannelDataSettingsConverter());
            return options;
        }
    }
}
