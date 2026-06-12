namespace RpCCAnalyticsConsole.Configuration;

public class AppSettings
{
    public AzureAISettings AzureAI { get; set; } = new();
    public ConnectionStringsSettings ConnectionStrings { get; set; } = new();
}

public class AzureAISettings
{
    public string Endpoint { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string ChatModel { get; set; } = "gpt-4o";
}

public class ConnectionStringsSettings
{
    public string CallCenter { get; set; } = "";
}
