using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Extensions.Teams.App;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using TeamsAgent;


WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient();
builder.Services.AddAgentAspNetAuthentication(builder.Configuration);
builder.Services.AddSingleton<IStorage, MemoryStorage>();
builder.Services.AddTransient(sp =>
{
    return new AgentApplicationOptions(sp.GetService<IStorage>()!)
    {
        StartTypingTimer = false,
        TurnStateFactory = () => new TurnState(sp.GetService<IStorage>()!),
        // new AttachmentDownloader(sp.GetService<IHttpClientFactory>()!),
        FileDownloaders = [new TeamsAttachmentDownloader(new TeamsAttachmentDownloaderOptions() 
                               {  
                                   TokenProviderName = "ServiceConnection" 
                               }, 
                               sp.GetService<IConnections>()!, 
                               sp.GetService<IHttpClientFactory>()!)]
    };
});
builder.AddAgent<TeamsAgent.TeamsAgent>();
WebApplication app = builder.Build();

app.MapPost("/api/messages",
    (HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
        adapter.ProcessAsync(request, response, agent, cancellationToken));
app.Run();