using Microsoft.Agents.Mcp.Server.GitHubMCPServer.Operations;
using Microsoft.Agents.Mcp.Core.DependencyInjection;
using Microsoft.Agents.Mcp.Server.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddHttpClient();

builder.Services.AddModelContextProtocolHandlers();
builder.Services.AddDefaultOperationFactory();
builder.Services.AddDefaultPayloadExecutionFactory();
builder.Services.AddDefaultPayloadResolver();
builder.Services.AddDefaultServerExecutors();
builder.Services.AddMemorySessionManager();
builder.Services.AddTransportManager();

builder.Services.AddToolExecutor<GitHubCreateRepoOperationExecutor>();
builder.Services.AddToolExecutor<GitHubCreateIssueOperationExecutor>(); 
builder.Services.AddToolExecutor<GitHubAddIssueCommentOperationExecutor>();
builder.Services.AddToolExecutor<GitHubIssueOperationExecutor>();
builder.Services.AddToolExecutor<GitHubListIssuesOperationExecutor>();
builder.Services.AddToolExecutor<GitHubUpdateIssueOperationExecutor>();

builder.Services.AddLogging();
builder.Logging.AddConsole();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseRouting();
app.MapControllers();
app.Run();