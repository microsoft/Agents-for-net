
using Microsoft.Agents.Mcp.Client.DependencyInjection;
using Microsoft.Agents.Mcp.Core.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddHttpClient();


builder.Services.AddModelContextProtocolHandlers();
builder.Services.AddDefaultPayloadExecutionFactory();
builder.Services.AddDefaultPayloadResolver();
builder.Services.AddDefaultClientExecutors();
builder.Services.AddMemorySessionManager();
builder.Services.AddTransportManager();

builder.Services.AddLogging();
builder.Logging.AddConsole();

var app = builder.Build();

app.UseHttpsRedirection();

app.UseRouting();
app.MapControllers();
app.Run();
