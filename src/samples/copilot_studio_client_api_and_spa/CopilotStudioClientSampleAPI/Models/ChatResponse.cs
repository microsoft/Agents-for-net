namespace CopilotStudioClientSampleAPI.Models;

public class Conversation
{
    public required string ConversationId { get; set; }
    public required List<ChatResponse> ChatResponses { get; set; }
}
public class ChatResponse
{
    public string? Role { get; set; }
    public List<Content>? Content { get; set; }
}
public class MessageRequest
{
    public required string Message { get; set; }
    public required string BotIdentifier { get; set; }
}

public class Content
{
    public required string Type { get; set; }
    public required string Text { get; set; }
}

