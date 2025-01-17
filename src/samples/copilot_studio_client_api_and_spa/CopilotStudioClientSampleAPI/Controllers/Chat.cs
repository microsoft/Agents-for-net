using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web.Resource;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using Microsoft.Agents.CopilotStudio.Client;
using Microsoft.Extensions.Logging;
using CopilotStudioClientSampleAPI.Services;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authentication;
using CopilotStudioClientSampleAPI.Models;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Text.Json;
using System.Collections.Generic;

namespace CopilotStudioClientSampleAPI.Controllers
{
    [Authorize]
    [RequiredScope(RequiredScopesConfigurationKey = "AzureAd:Scopes")]
    [Route("api/[controller]")]
    [ApiController]
    public class Chat : ControllerBase
    {
        private readonly CopilotConversationCache _copilotConversationCache; 
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<Chat> _logger;
        private readonly SampleConnectionSettings _settings;

        public Chat(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<Chat> logger, CopilotConversationCache copilotConversationCache)
        {
            _copilotConversationCache = copilotConversationCache;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
            _settings = new SampleConnectionSettings(_configuration.GetSection("DirectToEngineSettings"));
            // Validate _settings properties
            if (string.IsNullOrEmpty(_settings.TenantId) || string.IsNullOrEmpty(_settings.AppClientId) || string.IsNullOrEmpty(_settings.BotIdentifier))
            {
                throw new ArgumentException("BotIdentifier, TenantId and AppClientId in SampleConnectionSettings cannot be null or empty.");
            }
        }

        [HttpDelete]
        public IActionResult Delete(string botIdentifier)
        {
            try
            {
                var currentUser = User.Claims.Where(t => t.Type == "http://schemas.microsoft.com/identity/claims/objectidentifier").FirstOrDefault()?.Value;
                _copilotConversationCache.RemoveConversation(currentUser!, botIdentifier);
                return Ok();
            }
            catch
            {
                return BadRequest();
            }
        }

        [HttpPost]
        public async Task<IActionResult> PostAsync([FromBody] MessageRequest request)
        {
            JwtSecurityToken? jwtToken = ExtractJwtToken();
            if (jwtToken == null)
            {
                return Unauthorized();
            }

            // Get Configs
            var requestedScopes = _configuration.GetSection("API").Get<IEnumerable<string>>() ?? [];
            var clientId = _configuration.GetSection("AzureAd:ClientId").Value;
            var tenantId = _configuration.GetSection("AzureAd:TenantId").Value;
            var clientSecret = _configuration.GetSection("AzureAd:ClientSecret").Value;

            // Define the function to get the access token
            Func<string, Task<string>> tokenProviderFunction = async (url) =>
            {
                // Build Confidential Application
                var application = ConfidentialClientApplicationBuilder.Create(clientId)
                    .WithClientSecret(clientSecret)
                    .WithTenantId(tenantId)
                    .Build();

                // Aquire token on behalf of user
                UserAssertion userAssertion = new UserAssertion(jwtToken.RawData, "urn:ietf:params:oauth:grant-type:jwt-bearer");
                var result = await application.AcquireTokenOnBehalfOf(requestedScopes, userAssertion).ExecuteAsync();
                return result.AccessToken;
            };

            // Set the required agent in CopilotStudio
            if (!string.IsNullOrEmpty(request.BotIdentifier))
            {
                _settings.BotIdentifier = request.BotIdentifier;
            }

            // Create Copilot Client
            var copilotClient = new CopilotClient(_settings, _httpClientFactory, tokenProviderFunction, _logger, "mcs");
            
            // Get the current User
            var currentUser = User.Claims.Where(t => t.Type == "http://schemas.microsoft.com/identity/claims/objectidentifier").FirstOrDefault()?.Value;

            // Get the current conversation, null if not exist
            var conversationId = _copilotConversationCache.GetConversation(currentUser!, _settings.BotIdentifier!);

            var chatResponses = new List<ChatResponse>();
            try
            {
                var chatClient = new ChatConsoleService(copilotClient);
                using var cts = new CancellationTokenSource();
                // Get the CancellationToken from the CancellationTokenSource
                CancellationToken token = cts.Token;

                // If not conversation Start a conversation
                if (conversationId == null)
                {
                    var conversation = await chatClient.StartAsync(token);
                    chatResponses.AddRange(conversation.ChatResponses!);
                    _copilotConversationCache.AddConversation(currentUser!, _settings.BotIdentifier!, conversation.ConversationId!);
                }

                // If a message if avaialable sent it to the Copilot Studio Agent
                if (request.Message != string.Empty)
                {
                    var existingConversationId = _copilotConversationCache.GetConversation(currentUser!, _settings.BotIdentifier!);
                    var questionResponse = await chatClient.Ask(request.Message, existingConversationId!, token);
                    chatResponses.AddRange(questionResponse);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                return BadRequest("Error with communicating with Copilot Studio Agent");
            }

            string prettyJson = JsonSerializer.Serialize(chatResponses, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            return Content(prettyJson, "application/json");
        }

        private JwtSecurityToken? ExtractJwtToken()
        {
            // Get the raw JWT token from the Authorization header
            var authHeader = HttpContext.Request.Headers["Authorization"].ToString();
            var token = authHeader.StartsWith("Bearer ") ? authHeader.Substring("Bearer ".Length).Trim() : string.Empty;

            // Parse the token and cast it to JwtSecurityToken
            var handler = new JwtSecurityTokenHandler();
            try
            {
                var jwtToken = handler.ReadToken(token) as JwtSecurityToken;
                return jwtToken;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
