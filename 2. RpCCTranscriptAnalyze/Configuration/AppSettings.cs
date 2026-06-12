namespace RpCCTranscriptAnalyze.Configuration;

public class AppSettings
{
    public AzureAISettings AzureAI { get; set; } = new();
}

public class AzureAISettings
{
    /// <summary>Azure AI Foundry or Azure OpenAI endpoint URL.</summary>
    public string Endpoint { get; set; } = "";

    /// <summary>API key for the endpoint. Leave empty to use DefaultAzureCredential.</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>Deployment name for the chat model used to extract structured data.</summary>
    public string ChatModel { get; set; } = "gpt-4o";
}
