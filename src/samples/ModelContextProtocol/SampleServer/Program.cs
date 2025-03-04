using Microsoft.Agents.Mcp.Server.Sample.Operations;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Agents.Mcp.Core.DependencyInjection;
using Microsoft.Agents.Mcp.Server.DependencyInjection;

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

builder.Services.AddToolExecutor<MathAddOperationExecutor>();
builder.Services.AddToolExecutor<WeatherOperationExecutor>();
builder.Services.AddLogging();
builder.Logging.AddConsole();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseRouting();
app.MapControllers();
app.Run();
