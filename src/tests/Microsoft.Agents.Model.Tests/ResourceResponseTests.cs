﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Xunit;

namespace Microsoft.Agents.Model.Tests
{
    public class ResourceResponseTests
    {
        [Fact]
        public void ResponseResponseInit()
        {
            var id = "id";
            var resourceResponse = new ResourceResponse(id);

            Assert.NotNull(resourceResponse);
            Assert.IsType<ResourceResponse>(resourceResponse);
            Assert.Equal(id, resourceResponse.Id);
        }
        
        [Fact]
        public void ResponseResponseInitWithNoArgs()
        {
            var resourceResponse = new ResourceResponse();

            Assert.NotNull(resourceResponse);
            Assert.IsType<ResourceResponse>(resourceResponse);
        }
    }
}
