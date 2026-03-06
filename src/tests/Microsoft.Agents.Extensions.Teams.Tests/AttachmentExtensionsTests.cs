// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections;
using System.Collections.Generic;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.App.MessageExtensions;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests
{
    public class AttachmentExtensionsTests
    {
        [Theory]
        [ClassData(typeof(TestPreviewAttachment))]
        public void ToMessagingExtensionAttachment(Attachment previewAttachment)
        {
            var contentType = "contentType";
            var contentUrl = "http://my-content-url.com";
            var content = new { };
            var name = "name";
            var thumbnailUrl = "http://my-thumbnail-url.com";
            var attachment = new Attachment(contentType, contentUrl, new { }, name, thumbnailUrl);

            var messagingExtensionAttachment = AttachmentExtensions.ToMessagingExtensionAttachment(attachment, previewAttachment);

            Assert.NotNull(messagingExtensionAttachment);
            Assert.IsType<Microsoft.Teams.Api.MessageExtensions.Attachment>(messagingExtensionAttachment);
            Assert.Equal(contentType, messagingExtensionAttachment.ContentType);
            Assert.Equal(contentUrl, messagingExtensionAttachment.ContentUrl);
            Assert.Equal(content, messagingExtensionAttachment.Content);
            Assert.Equal(name, messagingExtensionAttachment.Name);
            Assert.Equal(thumbnailUrl, messagingExtensionAttachment.ThumbnailUrl);

            if (previewAttachment != null)
            {
                var previewAttachmentJson = ProtocolJsonSerializer.ToJson(previewAttachment);
                var teamsPreviewJson = ProtocolJsonSerializer.ToJson(ProtocolJsonSerializer.ToObject<Attachment>(messagingExtensionAttachment.Preview));
                Assert.Equal(previewAttachmentJson, teamsPreviewJson);
            }
        }

        internal class TestPreviewAttachment : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                yield return new object[] { new Attachment() { Name = "coreName", ContentType = "coreContentType", ContentUrl = "coreContentUrl" } };
                yield return new object[] { null };
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
