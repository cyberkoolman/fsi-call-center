using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using RpCCTranscriptAnalyze.Tools.Configuration;

namespace RpCCTranscriptAnalyze.Tools.Services;

/// <summary>
/// Wraps Azure AI Foundry / Azure OpenAI for both raw chat completions and
/// Microsoft Agent Framework (MAF) agents with auto function-calling.
/// </summary>
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
    /// Builds a Microsoft Agent Framework <see cref="AIAgent"/> backed by the
    /// Azure OpenAI chat deployment. Tools are exposed via <c>AIFunctionFactory.Create</c>
    /// and invoked automatically when the model emits a tool call.
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
