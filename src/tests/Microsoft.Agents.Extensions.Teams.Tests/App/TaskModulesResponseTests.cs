// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Teams.Api;
using Microsoft.Teams.Api.TaskModules;
using System;
using Xunit;
using Response = Microsoft.Agents.Extensions.Teams.App.TaskModules.Response;

namespace Microsoft.Agents.Extensions.Teams.Tests.App
{
    public class TaskModulesResponseTests
    {
        private static readonly Attachment _card = new Attachment("application/vnd.microsoft.card.adaptive", new { type = "AdaptiveCard" });

        // ── WithCard — input validation ────────────────────────────────────────

        [Fact]
        public void WithCard_NullCard_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => Response.WithCard(null!));
        }

        [Fact]
        public void WithCard_Size_NullCard_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => Response.WithCard(null!, title: null, Size.Medium, Size.Large));
        }

        // ── WithCard (pixel height/width) ──────────────────────────────────────

        [Fact]
        public void WithCard_ReturnsResponseWithContinueTask()
        {
            var result = Response.WithCard(_card);

            Assert.NotNull(result);
            Assert.IsType<ContinueTask>(result.Task);
            Assert.True(result.Task!.Type.IsContinue);
        }

        [Fact]
        public void WithCard_SetsCard()
        {
            var result = Response.WithCard(_card);

            var continueTask = Assert.IsType<ContinueTask>(result.Task);
            Assert.Same(_card, continueTask.Value!.Card);
        }

        [Fact]
        public void WithCard_DefaultsNullTitle()
        {
            var result = Response.WithCard(_card);

            var continueTask = Assert.IsType<ContinueTask>(result.Task);
            Assert.Null(continueTask.Value!.Title);
        }

        [Fact]
        public void WithCard_SetsTitle()
        {
            var result = Response.WithCard(_card, title: "My Dialog");

            var continueTask = Assert.IsType<ContinueTask>(result.Task);
            Assert.Equal("My Dialog", continueTask.Value!.Title);
        }

        [Fact]
        public void WithCard_DefaultsNullHeightAndWidth()
        {
            var result = Response.WithCard(_card);

            var continueTask = Assert.IsType<ContinueTask>(result.Task);
            Assert.Null(continueTask.Value!.Height);
            Assert.Null(continueTask.Value!.Width);
        }

        [Fact]
        public void WithCard_SetsPixelHeightAndWidth()
        {
            var result = Response.WithCard(_card, height: 400, width: 600);

            var continueTask = Assert.IsType<ContinueTask>(result.Task);
            var taskInfo = continueTask.Value!;
            Assert.Equal(400, taskInfo.Height!.Match(i => i, _ => -1));
            Assert.Equal(600, taskInfo.Width!.Match(i => i, _ => -1));
        }

        [Fact]
        public void WithCard_DefaultsNullFallbackUrl()
        {
            var result = Response.WithCard(_card);

            var continueTask = Assert.IsType<ContinueTask>(result.Task);
            Assert.Null(continueTask.Value!.FallbackUrl);
        }

        [Fact]
        public void WithCard_SetsFallbackUrl()
        {
            var result = Response.WithCard(_card, fallbackUrl: "https://example.com/fallback");

            var continueTask = Assert.IsType<ContinueTask>(result.Task);
            Assert.Equal("https://example.com/fallback", continueTask.Value!.FallbackUrl);
        }

        [Fact]
        public void WithCard_DefaultsNullCacheInfo()
        {
            var result = Response.WithCard(_card);

            Assert.Null(result.CacheInfo);
        }

        [Fact]
        public void WithCard_SetsCacheInfo()
        {
            var cacheInfo = new CacheInfo { CacheType = "some_type", CacheDuration = 60 };

            var result = Response.WithCard(_card, cacheInfo: cacheInfo);

            Assert.NotNull(result.CacheInfo);
            Assert.Equal("some_type", result.CacheInfo!.CacheType);
            Assert.Equal(60, result.CacheInfo!.CacheDuration);
        }

        [Fact]
        public void WithCard_DoesNotSetUrl()
        {
            var result = Response.WithCard(_card);

            var continueTask = Assert.IsType<ContinueTask>(result.Task);
            Assert.Null(continueTask.Value!.Url);
        }

        [Fact]
        public void WithCard_DoesNotSetCompletionBotId()
        {
            var result = Response.WithCard(_card);

            var continueTask = Assert.IsType<ContinueTask>(result.Task);
            Assert.Null(continueTask.Value!.CompletionBotId);
        }

        // ── WithCard (Size height/width) ───────────────────────────────────────

        [Fact]
        public void WithCard_SetsSizeHeightAndWidth()
        {
            var result = Response.WithCard(_card, title: null, height: Size.Large, width: Size.Small);

            var continueTask = Assert.IsType<ContinueTask>(result.Task);
            var taskInfo = continueTask.Value!;
            Assert.Equal(Size.Large, taskInfo.Height!.Match(_ => Size.Large, s => s));
            Assert.Equal(Size.Small, taskInfo.Width!.Match(_ => Size.Small, s => s));
        }

        [Fact]
        public void WithCard_Size_SetsCardTitleFallbackUrlAndCacheInfo()
        {
            var cacheInfo = new CacheInfo { CacheDuration = 30 };

            var result = Response.WithCard(_card, title: "Dialog", Size.Medium, Size.Large, fallbackUrl: "https://fb.example.com", cacheInfo: cacheInfo);

            var continueTask = Assert.IsType<ContinueTask>(result.Task);
            var taskInfo = continueTask.Value!;
            Assert.Same(_card, taskInfo.Card);
            Assert.Equal("Dialog", taskInfo.Title);
            Assert.Equal("https://fb.example.com", taskInfo.FallbackUrl);
            Assert.Equal(30, result.CacheInfo!.CacheDuration);
        }

        // ── WithUrl — input validation ─────────────────────────────────────────

        [Fact]
        public void WithUrl_NullUrl_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => Response.WithUrl(null!));
        }

        [Fact]
        public void WithUrl_WhitespaceUrl_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => Response.WithUrl("   "));
        }

        [Fact]
        public void WithUrl_Size_NullUrl_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => Response.WithUrl(null!, title: null, Size.Small, Size.Large));
        }

        [Fact]
        public void WithUrl_Size_WhitespaceUrl_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => Response.WithUrl("   ", title: null, Size.Small, Size.Large));
        }

        // ── WithUrl (pixel height/width) ───────────────────────────────────────

        [Fact]
        public void WithUrl_ReturnsResponseWithContinueTask()
        {
            var result = Response.WithUrl("https://example.com/form");

            Assert.NotNull(result);
            Assert.IsType<ContinueTask>(result.Task);
            Assert.True(result.Task!.Type.IsContinue);
        }

        [Fact]
        public void WithUrl_SetsUrl()
        {
            var result = Response.WithUrl("https://example.com/form");

            var continueTask = Assert.IsType<ContinueTask>(result.Task);
            Assert.Equal("https://example.com/form", continueTask.Value!.Url);
        }

        [Fact]
        public void WithUrl_DefaultsNullOptionalParams()
        {
            var result = Response.WithUrl("https://example.com/form");

            var continueTask = Assert.IsType<ContinueTask>(result.Task);
            var taskInfo = continueTask.Value!;
            Assert.Null(taskInfo.Title);
            Assert.Null(taskInfo.Height);
            Assert.Null(taskInfo.Width);
            Assert.Null(taskInfo.FallbackUrl);
            Assert.Null(taskInfo.CompletionBotId);
            Assert.Null(result.CacheInfo);
        }

        [Fact]
        public void WithUrl_SetsAllOptionalParams()
        {
            var cacheInfo = new CacheInfo { CacheDuration = 120 };

            var result = Response.WithUrl(
                "https://example.com/form",
                title: "Form Dialog",
                height: 500,
                width: 700,
                fallbackUrl: "https://example.com/fallback",
                completionBotId: "bot-app-id",
                cacheInfo: cacheInfo);

            var continueTask = Assert.IsType<ContinueTask>(result.Task);
            var taskInfo = continueTask.Value!;
            Assert.Equal("Form Dialog", taskInfo.Title);
            Assert.Equal(500, taskInfo.Height!.Match(i => i, _ => -1));
            Assert.Equal(700, taskInfo.Width!.Match(i => i, _ => -1));
            Assert.Equal("https://example.com/fallback", taskInfo.FallbackUrl);
            Assert.Equal("bot-app-id", taskInfo.CompletionBotId);
            Assert.Equal(120, result.CacheInfo!.CacheDuration);
        }

        // ── WithUrl (Size height/width) ────────────────────────────────────────

        [Fact]
        public void WithUrl_SetsSizeHeightAndWidth()
        {
            var result = Response.WithUrl("https://example.com/form", title: null, Size.Medium, Size.Large);

            var continueTask = Assert.IsType<ContinueTask>(result.Task);
            var taskInfo = continueTask.Value!;
            Assert.Equal(Size.Medium, taskInfo.Height!.Match(_ => Size.Medium, s => s));
            Assert.Equal(Size.Large, taskInfo.Width!.Match(_ => Size.Large, s => s));
        }

        [Fact]
        public void WithUrl_Size_SetsAllOptionalParams()
        {
            var cacheInfo = new CacheInfo { CacheDuration = 90 };

            var result = Response.WithUrl(
                "https://example.com/form",
                title: "Size Dialog",
                Size.Small,
                Size.Large,
                fallbackUrl: "https://example.com/fb",
                completionBotId: "completion-bot",
                cacheInfo: cacheInfo);

            var continueTask = Assert.IsType<ContinueTask>(result.Task);
            var taskInfo = continueTask.Value!;
            Assert.Equal("Size Dialog", taskInfo.Title);
            Assert.Equal("https://example.com/fb", taskInfo.FallbackUrl);
            Assert.Equal("completion-bot", taskInfo.CompletionBotId);
            Assert.Equal(90, result.CacheInfo!.CacheDuration);
        }

        // ── WithMessage — input validation ─────────────────────────────────────

        [Fact]
        public void WithMessage_NullMessage_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => Response.WithMessage(null!));
        }

        [Fact]
        public void WithMessage_WhitespaceMessage_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => Response.WithMessage("   "));
        }

        // ── WithMessage ────────────────────────────────────────────────────────

        [Fact]
        public void WithMessage_ReturnsResponseWithMessageTask()
        {
            var result = Response.WithMessage("Task complete!");

            Assert.NotNull(result);
            Assert.IsType<MessageTask>(result.Task);
            Assert.True(result.Task!.Type.IsMessage);
        }

        [Fact]
        public void WithMessage_SetsMessageValue()
        {
            var result = Response.WithMessage("Task complete!");

            var messageTask = Assert.IsType<MessageTask>(result.Task);
            Assert.Equal("Task complete!", messageTask.Value);
        }

        [Fact]
        public void WithMessage_DefaultsNullCacheInfo()
        {
            var result = Response.WithMessage("Done");

            Assert.Null(result.CacheInfo);
        }

        [Fact]
        public void WithMessage_SetsCacheInfo()
        {
            var cacheInfo = new CacheInfo { CacheType = "result", CacheDuration = 300 };

            var result = Response.WithMessage("Done", cacheInfo: cacheInfo);

            Assert.NotNull(result.CacheInfo);
            Assert.Equal("result", result.CacheInfo!.CacheType);
            Assert.Equal(300, result.CacheInfo!.CacheDuration);
        }
    }
}
