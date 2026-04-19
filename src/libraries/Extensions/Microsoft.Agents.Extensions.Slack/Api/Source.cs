// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Slack.Api;

public class Source
{
    public string type { get; set; } = "url";

    public string url { get; set; } = "";

    public string text { get; set; } = "";
}

