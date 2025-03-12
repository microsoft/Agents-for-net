namespace Microsoft.Agents.Mcp.Server.GitHubMCPServer.DataModel
{
    public class GitHubFileOperationsInput
    {
        public string Owner { get; set; } = "";
        public string Repo { get; set; } = "";
        public string Path { get; set; } = "";
        public string OperationType { get; set; } = "";
        public string? Branch { get; set; }
        public string? Content { get; set; }
        public string? Message { get; set; }
    }

    public class GitHubFileOperationsOutput
    {
        public string? FileContent { get; set; }
        public string? Response { get; set; }
    }

    public class GitHubFileResponse
    {
        public string? Content { get; set; }
        public string? Sha { get; set; }
    }
}
