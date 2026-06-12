using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using OpenAI.Chat;
using RpCCTranscriptAnalyze.Configuration;
using System.Text.Json;

namespace RpCCTranscriptAnalyze.Services;

/// <summary>
/// Wraps Azure AI Foundry / Azure OpenAI for chat completions.
/// Reads endpoint and API key from AppSettings so credentials never appear in code.
/// </summary>
public class AzureAIService
{
    private readonly AzureOpenAIClient _client;
    private readonly AzureAISettings _settings;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

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
    /// Sends a single system + user message pair and returns the assistant's text.
    /// </summary>
    public async Task<string> CompleteAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        var chatClient = _client.GetChatClient(_settings.ChatModel);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userMessage)
        };

        var response = await chatClient.CompleteChatAsync(messages, cancellationToken: cancellationToken);
        return response.Value.Content[0].Text;
    }

    /// <summary>
    /// Like CompleteAsync but deserialises the response JSON into <typeparamref name="T"/>.
    /// The system prompt is automatically appended with a JSON-only instruction.
    /// </summary>
    public async Task<T?> CompleteStructuredAsync<T>(
        string systemPrompt,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        const string jsonSuffix =
            "\n\nIMPORTANT: Respond with valid JSON only. No markdown fences, no explanations — just the JSON object.";

        var raw = await CompleteAsync(
            systemPrompt + jsonSuffix,
            userMessage,
            cancellationToken);

        var json = StripMarkdownFences(raw);

        try
        {
            return JsonSerializer.Deserialize<T>(json, _jsonOptions);
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[AzureAIService] JSON parse error: {ex.Message}\nRaw: {json[..Math.Min(200, json.Length)]}");
            return default;
        }
    }

    private static string StripMarkdownFences(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("```")) return trimmed;

        var firstNewline = trimmed.IndexOf('\n');
        if (firstNewline < 0) return trimmed;

        var withoutOpen = trimmed[(firstNewline + 1)..];

        var lastFence = withoutOpen.LastIndexOf("```");
        if (lastFence >= 0)
            withoutOpen = withoutOpen[..lastFence];

        return withoutOpen.Trim();
    }
}
