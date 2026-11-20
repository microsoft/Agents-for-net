// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using MessageExtensions;
using Microsoft.Agents.Builder.Testing.Http;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using TaskModules;
using Xunit;

namespace Microsoft.Agents.SampleTest;

public class TeamsInvokeSampleTests
{
    [Fact]
    public async Task MessageExtension_SearchBuildsCardsFromQueryWithoutHttp()
    {
        using var http = new HttpSimulation();
        await using (var host = HttpSampleHost.Create(http,
            (options, _) => new MessageExtensionsAgent(options), Channels.Msteams))
        {
            await host.CreateTestFlow()
                .Send(HttpSampleHost.TeamsInvoke("composeExtension/query", new
                {
                    commandId = "searchQuery",
                    parameters = new[] { new { name = "searchQuery", value = "synthetic-query" } }
                }))
                .AssertReply(activity =>
                {
                    var body = InvokeBody(activity);
                    var result = body.GetProperty("composeExtension");
                    Assert.Equal("result", result.GetProperty("type").GetString());
                    var attachments = result.GetProperty("attachments");
                    Assert.Equal(5, attachments.GetArrayLength());
                    Assert.Contains("synthetic-query", attachments[0].GetRawText());
                })
                .StartTestAsync();
        }
        http.Verify();
    }

    [Fact]
    public async Task MessageExtension_InitialRunReturnsPromptWithoutHttp()
    {
        using var http = new HttpSimulation();
        await using (var host = HttpSampleHost.Create(http,
            (options, _) => new MessageExtensionsAgent(options), Channels.Msteams))
        {
            await host.CreateTestFlow()
                .Send(HttpSampleHost.TeamsInvoke("composeExtension/query", new
                {
                    commandId = "searchQuery",
                    parameters = new[] { new { name = "initialRun", value = "true" } }
                }))
                .AssertReply(activity =>
                {
                    var result = InvokeBody(activity).GetProperty("composeExtension");
                    Assert.Equal("message", result.GetProperty("type").GetString());
                    Assert.Equal("Enter search query", result.GetProperty("text").GetString());
                })
                .StartTestAsync();
        }
        http.Verify();
    }

    [Fact]
    public async Task MessageExtension_LinkPreviewDoesNotDownloadLinkedContent()
    {
        using var http = new HttpSimulation();
        await using (var host = HttpSampleHost.Create(http,
            (options, _) => new MessageExtensionsAgent(options), Channels.Msteams))
        {
            await host.CreateTestFlow()
                .Send(HttpSampleHost.TeamsInvoke("composeExtension/queryLink", new { url = "https://links.example.test/item" }))
                .AssertReply(activity =>
                {
                    var result = InvokeBody(activity).GetProperty("composeExtension");
                    Assert.Equal("result", result.GetProperty("type").GetString());
                    Assert.Contains("https://links.example.test/item", result.GetProperty("attachments")[0].GetRawText());
                })
                .StartTestAsync();
        }
        http.Verify();
    }

    [Fact]
    public async Task TaskModule_FetchReturnsEmbeddedFormWithoutHttp()
    {
        using var http = new HttpSimulation();
        await using (var host = HttpSampleHost.Create(http,
            (options, _) => new TaskModulesAgent(options, new ConfigurationBuilder().Build()), Channels.Msteams))
        {
            await host.CreateTestFlow()
                .Send(HttpSampleHost.TeamsInvoke("task/fetch", new { data = new { task = "simple_form" } }))
                .AssertReply(activity =>
                {
                    var task = InvokeBody(activity).GetProperty("task");
                    Assert.Equal("continue", task.GetProperty("type").GetString());
                    Assert.Equal("Simple Form", task.GetProperty("value").GetProperty("title").GetString());
                    Assert.Equal(ContentTypes.AdaptiveCard,
                        task.GetProperty("value").GetProperty("card").GetProperty("contentType").GetString());
                })
                .StartTestAsync();
        }
        http.Verify();
    }

    [Fact]
    public async Task TaskModule_SubmitSendsReplyAndInvokeAcknowledgment()
    {
        using var http = new HttpSimulation();
        await using (var host = HttpSampleHost.Create(http,
            (options, _) => new TaskModulesAgent(options, new ConfigurationBuilder().Build()), Channels.Msteams))
        {
            await host.CreateTestFlow()
                .Send(HttpSampleHost.TeamsInvoke("task/submit", new
                {
                    data = new { task = "simple_form", name = "Synthetic User" }
                }))
                .AssertReply("Hi Synthetic User, thanks for submitting the form!")
                .AssertReply(activity =>
                {
                    var task = InvokeBody(activity).GetProperty("task");
                    Assert.Equal("message", task.GetProperty("type").GetString());
                    Assert.Equal("Form was submitted", task.GetProperty("value").GetString());
                })
                .StartTestAsync();
        }
        http.Verify();
    }

    [Fact]
    public async Task TaskModule_WebpageUsesConfiguredUrlWithoutFetchingIt()
    {
        using var http = new HttpSimulation();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            ["AppBaseUrl"] = "https://forms.example.test"
        }).Build();
        await using (var host = HttpSampleHost.Create(http,
            (options, _) => new TaskModulesAgent(options, configuration), Channels.Msteams))
        {
            await host.CreateTestFlow()
                .Send(HttpSampleHost.TeamsInvoke("task/fetch", new { data = new { task = "webpage_dialog" } }))
                .AssertReply(activity => Assert.Equal("https://forms.example.test/dialog-form",
                    InvokeBody(activity).GetProperty("task").GetProperty("value").GetProperty("url").GetString()))
                .StartTestAsync();
        }
        http.Verify();
    }

    [Fact]
    public async Task TaskModule_MultiStepFormCarriesNameIntoCompletion()
    {
        using var http = new HttpSimulation();
        await using (var host = HttpSampleHost.Create(http,
            (options, _) => new TaskModulesAgent(options, new ConfigurationBuilder().Build()), Channels.Msteams))
        {
            await host.CreateTestFlow()
                .Send(HttpSampleHost.TeamsInvoke("task/submit", new
                {
                    data = new { task = "multi_step_form_submit_name", name = "Synthetic User" }
                }))
                .AssertReply(activity =>
                {
                    var task = InvokeBody(activity).GetProperty("task");
                    Assert.Equal("continue", task.GetProperty("type").GetString());
                    Assert.Equal("Thanks Synthetic User - Get Email", task.GetProperty("value").GetProperty("title").GetString());
                    Assert.Contains("Synthetic User", task.GetProperty("value").GetProperty("card").GetRawText());
                })
                .Send(HttpSampleHost.TeamsInvoke("task/submit", new
                {
                    data = new
                    {
                        task = "multi_step_form_submit_email",
                        name = "Synthetic User",
                        email = "synthetic@example.test"
                    }
                }))
                .AssertReply("Hi Synthetic User, thanks for submitting the form! We got that your email is synthetic@example.test")
                .AssertReply(activity => Assert.Equal("Multi-step form completed successfully",
                    InvokeBody(activity).GetProperty("task").GetProperty("value").GetString()))
                .StartTestAsync();
        }
        http.Verify();
    }

    private static JsonElement InvokeBody(IActivity activity)
    {
        Assert.Equal(ActivityTypes.InvokeResponse, activity.Type);
        var response = ProtocolJsonSerializer.ToObject<InvokeResponse>(activity.Value);
        Assert.Equal(200, response.Status);
        return ProtocolJsonSerializer.ToObject<JsonElement>(response.Body);
    }
}
