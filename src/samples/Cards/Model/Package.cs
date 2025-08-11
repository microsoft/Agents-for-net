// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Cards.Model;

/// <summary>
/// The strongly typed NuGet package search result
/// </summary>
internal class Package
{
    public required string Id { get; set; }

    public required string Description { get; set; }
}
