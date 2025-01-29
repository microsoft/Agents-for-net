namespace CopilotStudioClientSampleAPI.Services
{
    public class CopilotConversationCache
    {
        private readonly Dictionary<string, string> _conversations;

        public CopilotConversationCache()
        {
            _conversations = new Dictionary<string, string>();
        }

        public string? GetConversation(string key, string botIdentifier)
        {
            if (_conversations.TryGetValue(CreateCombinedKey(key, botIdentifier), out var conversationId ))
            {
                return conversationId;
            }
            return null;
        }

        public void AddConversation(string key, string botIdentifier, string conversationId)
        {
            _conversations.Add(CreateCombinedKey(key, botIdentifier), conversationId);
        }

        public void RemoveConversation(string key, string botIdentifier)
        {
            _conversations.Remove(CreateCombinedKey(key, botIdentifier));
        }

        private string CreateCombinedKey(string key1, string key2)
        {
            return $"{key1}_{key2}";
        }
    }
}