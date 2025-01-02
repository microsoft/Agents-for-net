using Microsoft.Agents.Authentication.Msal;
using Microsoft.Agents.Authentication.Msal.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http;
using Xunit.Abstractions;

namespace Microsoft.CopilotStudio.Connector.Tests
{
    internal class SetupServiceCollection
    {
        internal static ServiceProvider GenerateAuthMinServiceProvider(string settingsFile, ITestOutputHelper output)
        {
            IConfiguration config = new ConfigurationBuilder()
               .AddJsonFile(
                   System.IO.Path.Combine("Resources", settingsFile),
                   optional: false,
                   reloadOnChange: true)
               .Build();

            ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
                    builder.AddSimpleConsole(options =>
                    {
                        options.IncludeScopes = true;
                        options.TimestampFormat = "hh:mm:ss ";
                    })
                    .AddConfiguration(config.GetSection("Logging"))
                    .AddProvider(new TraceConsoleLoggingProvider(output)));

            var services = new ServiceCollection();
            services.AddSingleton(loggerFactory);
            services.AddSingleton(config);

            services.AddDefaultMsalAuth(config); 
            
            return services.BuildServiceProvider();
        }
    }
}