// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.SharePoint.Serialization;

// Allows us to access some internal methods from the Memory.Tests unit tests so we don't have to use reflection and we get compile checks.
[assembly: SerializationInit(typeof(SerializationInit))]