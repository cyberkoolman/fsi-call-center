using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using OpenAI.Chat;
using RpCCKbRAG.Configuration;
using System.Text.Json;

namespace RpCCKbRAG.Services;

/// <summary>
/// Wraps Azure AI Foundry / Azure OpenAI for chat completions and text embeddings.
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

    public async Task<string> CompleteAsync(
        string systemPrompt,
        string userMessage,
        bool useReasoningModel = false,
        CancellationToken cancellationToken = default)
    {
        var model = useReasoningModel ? _settings.ReasoningModel : _settings.FastModel;
        var chatClient = _client.GetChatClient(model);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userMessage)
        };

        var response = await chatClient.CompleteChatAsync(messages, cancellationToken: cancellationToken);
        return response.Value.Content[0].Text;
    }

    public async Task<T?> CompleteStructuredAsync<T>(
        string systemPrompt,
        string userMessage,
        bool useReasoningModel = false,
        CancellationToken cancellationToken = default)
    {
        const string jsonSuffix =
            "\n\nIMPORTANT: Respond with valid JSON only. No markdown fences, no explanations — just the JSON object.";

        var raw = await CompleteAsync(
            systemPrompt + jsonSuffix,
            userMessage,
            useReasoningModel,
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

    public async Task<float[]> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var embeddingClient = _client.GetEmbeddingClient(_settings.EmbeddingModel);
            var response = await embeddingClient.GenerateEmbeddingAsync(text, cancellationToken: cancellationToken);
            return response.Value.ToFloats().ToArray();
        }
        catch (System.ClientModel.ClientResultException ex) when (ex.Status == 404)
        {
            throw new InvalidOperationException(
                $"Embedding deployment '{_settings.EmbeddingModel}' not found. " +
                "Check 'AzureAI:EmbeddingModel' in appsettings.json matches an active deployment.", ex);
        }
    }

    public async Task<List<float[]>> GenerateEmbeddingsAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var embeddingClient = _client.GetEmbeddingClient(_settings.EmbeddingModel);
            var textList = texts.ToList();
            var response = await embeddingClient.GenerateEmbeddingsAsync(textList, cancellationToken: cancellationToken);
            return response.Value.Select(e => e.ToFloats().ToArray()).ToList();
        }
        catch (System.ClientModel.ClientResultException ex) when (ex.Status == 404)
        {
            throw new InvalidOperationException(
                $"Embedding deployment '{_settings.EmbeddingModel}' not found. " +
                "Check 'AzureAI:EmbeddingModel' in appsettings.json matches an active deployment.", ex);
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
