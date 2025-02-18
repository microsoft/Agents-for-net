using Microsoft.Agents.BotBuilder.State;
using Microsoft.Agents.Extensions.Teams.App;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Samples;
using Microsoft.Agents.Storage;
using SearchCommand;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Logging.AddConsole();

// Add AspNet token validation
builder.Services.AddBotAspNetAuthentication(builder.Configuration);

builder.Services.AddSingleton<ActivityHandlers>();

// Create the bot as a transient. In this case the ASP Controller is expecting an IBot.
builder.AddBot(sp =>
{
    TeamsApplicationOptions applicationOptions = new()
    {
        TurnStateFactory = () => new TurnState(sp.GetService<IStorage>()!)
    };

    TeamsApplication app = new(applicationOptions);

    ActivityHandlers activityHandlers = sp.GetService<ActivityHandlers>()!;

    // Listen for search actions
    app.MessageExtensions.OnQuery("searchCmd", activityHandlers.QueryHandler);
    // Listen for item tap
    app.MessageExtensions.OnSelectItem(activityHandlers.SelectItemHandler);

    return app;
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseStaticFiles();
app.UseRouting();
app.MapControllers();

app.Run();
