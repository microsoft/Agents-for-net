namespace CopilotStudioClientSampleAPI.Models
{
    public class AzureAdSettings
    {
        public string Instance { get; set; }
        public string TenantId { get; set; }
        public string ClientId { get; set; }
        public string CallbackPath { get; set; }
        public string Scopes { get; set; }
        public string ClientSecret { get; set; }
        public List<string> ClientCertificates { get; set; }
    }
}
