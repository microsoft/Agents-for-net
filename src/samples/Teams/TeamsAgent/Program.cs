using Microsoft.Agents.Builder;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using TeamsAgent;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient();
builder.Services.AddAgentAspNetAuthentication(builder.Configuration);
builder.AddAgentApplicationOptions();
builder.AddAgent<TeamsAgent.TeamsAgent>();
builder.Services.AddSingleton<IStorage, MemoryStorage>();
WebApplication app = builder.Build();

app.MapPost("/api/messages",
    (HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
        adapter.ProcessAsync(request, response, agent, cancellationToken));
app.Run();