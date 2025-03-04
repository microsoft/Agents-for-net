﻿// Copyright(c) Microsoft Corporation.All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Extensions.Teams.Models;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.Model
{
    public class O365ConnectorCardImageTests
    {
        [Fact]
        public void O365ConnectorCardImageInits()
        {
            var image = "http://example-image.com";
            var title = "Happy Pandas";

            var cardImage = new O365ConnectorCardImage(image, title);

            Assert.NotNull(cardImage);
            Assert.IsType<O365ConnectorCardImage>(cardImage);
            Assert.Equal(image, cardImage.Image);
            Assert.Equal(title, cardImage.Title);
        }

        [Fact]
        public void O365ConnectorCardImageInitsWithNoArgs()
        {
            var cardImage = new O365ConnectorCardImage();

            Assert.NotNull(cardImage);
            Assert.IsType<O365ConnectorCardImage>(cardImage);
        }
    }
}
