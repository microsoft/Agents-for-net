// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Connector.RestClients;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Connector.Tests
{
    public class RestRequestTests
    {
        private static readonly Uri Base = new Uri("http://localhost/");

        [Fact]
        public void Get_Build_SetsMethodAndUri()
        {
            using var msg = RestRequest.Get("v3/conversations").Build(Base);
            Assert.Equal(HttpMethod.Get, msg.Method);
            Assert.Equal("http://localhost/v3/conversations", msg.RequestUri!.ToString());
        }

        [Fact]
        public void Post_Build_SetsMethodAndUri()
        {
            using var msg = RestRequest.Post("v3/conversations").Build(Base);
            Assert.Equal(HttpMethod.Post, msg.Method);
        }

        [Fact]
        public void Put_Build_SetsMethod()
        {
            using var msg = RestRequest.Put("v3/conversations/x/activities/y").Build(Base);
            Assert.Equal(HttpMethod.Put, msg.Method);
        }

        [Fact]
        public void Delete_Build_SetsMethod()
        {
            using var msg = RestRequest.Delete("v3/conversations/x/activities/y").Build(Base);
            Assert.Equal(HttpMethod.Delete, msg.Method);
        }

        [Fact]
        public void Get_NullPath_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => RestRequest.Get(null));
        }

        [Fact]
        public void Post_NullPath_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => RestRequest.Post(null));
        }

        [Fact]
        public void Put_NullPath_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => RestRequest.Put(null));
        }

        [Fact]
        public void Delete_NullPath_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => RestRequest.Delete(null));
        }

        [Fact]
        public void Get_EmptyPath_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => RestRequest.Get(""));
        }

        [Fact]
        public void Build_NullBaseUri_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => RestRequest.Get("v3/conversations").Build(null));
        }

        [Fact]
        public void WithQuery_NullName_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => RestRequest.Get("v3/conversations").WithQuery(null, "value"));
        }

        [Fact]
        public void WithQuery_EmptyName_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => RestRequest.Get("v3/conversations").WithQuery("", "value"));
        }

        [Fact]
        public void Build_AddsAcceptApplicationJson()
        {
            using var msg = RestRequest.Get("v3/conversations").Build(Base);
            Assert.Contains(msg.Headers.Accept, h => h.MediaType == "application/json");
        }

        [Fact]
        public void Build_WithQuery_AppendsQueryString()
        {
            using var msg = RestRequest.Get("v3/conversations")
                .WithQuery("continuationToken", "abc")
                .Build(Base);
            Assert.Contains("continuationToken=abc", msg.RequestUri!.Query);
        }

        [Fact]
        public void Build_WithQuery_NullValue_OmitsParameter()
        {
            using var msg = RestRequest.Get("v3/conversations")
                .WithQuery("continuationToken", null)
                .Build(Base);
            Assert.DoesNotContain("continuationToken", msg.RequestUri!.Query);
        }

        [Fact]
        public void Build_WithBody_SetsJsonContent()
        {
            var body = new { Name = "Test" };
            using var msg = RestRequest.Post("v3/conversations")
                .WithBody(body)
                .Build(Base);
            Assert.NotNull(msg.Content);
            Assert.Equal("application/json", msg.Content!.Headers.ContentType?.MediaType);
        }

        [Fact]
        public void Build_NullBody_NoContent()
        {
            using var msg = RestRequest.Post("v3/conversations")
                .WithBody<object>(null)
                .Build(Base);
            Assert.Null(msg.Content);
        }

        [Fact]
        public void Build_BaseUriWithoutTrailingSlash_StillBuildsCorrectly()
        {
            var baseNoSlash = new Uri("http://localhost");
            using var msg = RestRequest.Get("v3/conversations").Build(baseNoSlash);
            Assert.Equal("http://localhost/v3/conversations", msg.RequestUri!.ToString());
        }

        [Fact]
        public void Build_MultipleQueryParams_AllAppended()
        {
            using var msg = RestRequest.Get("api/usertoken/GetToken")
                .WithQuery("userId", "u1")
                .WithQuery("connectionName", "c1")
                .WithQuery("channelId", "msteams")
                .Build(Base);
            var query = msg.RequestUri!.Query;
            Assert.Contains("userId=u1", query);
            Assert.Contains("connectionName=c1", query);
            Assert.Contains("channelId=msteams", query);
        }

#if NET8_0_OR_GREATER
        [Fact]
        public async Task Build_WithActivityBinaryAttachments_MovesStreamsToRequestOptions()
        {
            var stream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
            var bytes = new byte[] { 5, 6, 7 };
            var activity = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "with stream",
                Attachments =
                [
                    new Attachment { ContentType = "image/png", Content = stream },
                    new Attachment { ContentType = "audio/wav", Content = bytes },
                    new Attachment { ContentType = "application/json", Content = new { Name = "inline" } },
                ],
            };

            using var msg = RestRequest.Post("v3/conversations/c/activities")
                .WithBody(activity)
                .Build(Base);

            Assert.True(msg.Options.TryGetValue(RestRequest.StreamingAttachmentsOption, out var streamContents));
            Assert.Equal(2, streamContents.Count);
            Assert.Equal("image/png", streamContents[0].ContentType);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, streamContents[0].Body);
            Assert.Equal("audio/wav", streamContents[1].ContentType);
            Assert.Equal(new byte[] { 5, 6, 7 }, streamContents[1].Body);
            Assert.Equal(0, stream.Position);

            var json = await msg.Content!.ReadAsStringAsync();
            var serializedActivity = JsonSerializer.Deserialize<Activity>(json, ProtocolJsonSerializer.SerializationOptions);
            var attachment = Assert.Single(serializedActivity!.Attachments);
            Assert.Equal("application/json", attachment.ContentType);

            Assert.Equal(3, activity.Attachments.Count);
            Assert.Same(stream, activity.Attachments[0].Content);
            Assert.Same(bytes, activity.Attachments[1].Content);
        }
#endif
    }
}
