﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Xunit;

namespace Microsoft.Agents.Model.Tests
{
    public class SpeechConstantsTests
    {
        [Fact]
        public void EmptySpeakTag()
        {
            var expected = "<speak version=\"1.0\" xmlns=\"https://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\" />";

            Assert.Equal(expected, SpeechConstants.EmptySpeakTag);
        }
    }
}
