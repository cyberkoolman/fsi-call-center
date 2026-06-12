using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using RpCCAnalyticsConsole.Configuration;

namespace RpCCAnalyticsConsole.Services;

public class AzureAIService
{
    private readonly AzureOpenAIClient _client;
    private readonly AzureAISettings _settings;

    public AzureAIService(AppSettings settings)
    {
        _settings = settings.AzureAI;

        if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            _client = new AzureOpenAIClient(
                new Uri(_settings.Endpoint),
                new AzureKeyCredential(_settings.ApiKey));
        }
        else
        {
            _client = new AzureOpenAIClient(
                new Uri(_settings.Endpoint),
                new DefaultAzureCredential());
        }
    }

    /// <summary>
    /// Builds a Microsoft Agent Framework agent backed by the Azure OpenAI
    /// chat deployment. Tools are auto-invoked when the model emits tool calls.
    /// </summary>
    public AIAgent CreateAgent(string instructions, string name, IEnumerable<AITool>? tools = null)
    {
        var chatClient = _client.GetChatClient(_settings.ChatModel);
        return chatClient.AsAIAgent(
            instructions: instructions,
            name: name,
            tools: tools?.ToList());
    }
}
