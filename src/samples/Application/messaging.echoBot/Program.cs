using EchoBot;
using Microsoft.Agents.BotBuilder.App;
using Microsoft.Agents.BotBuilder.State;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Samples;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Logging.AddConsole();

// Add AspNet token validation
builder.Services.AddBotAspNetAuthentication(builder.Configuration);

// Create the bot as a transient.
builder.Services.AddTransient<ApplicationOptions>(sp =>
{
    return new()
    {
        StartTypingTimer = true,
        TurnStateFactory = () => new TurnState(sp.GetService<IStorage>())
    };
});

builder.AddBot<EchoBotApplication>();


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapGet("/", () => "Microsoft Agents SDK Sample");
    app.UseDeveloperExceptionPage();
    app.MapControllers().AllowAnonymous();
}
else
{
    app.MapControllers();
}
app.Run();

