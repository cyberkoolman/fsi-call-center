using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using OpenAI.Chat;
using RpCCKbRAG.Configuration;

namespace RpCCKbRAG.Services;

/// <summary>
/// Thin wrapper around Azure OpenAI for chat completions and text embeddings.
/// </summary>
public class AzureAIService
{
    private readonly AzureOpenAIClient _client;
    private readonly AzureAISettings   _settings;

    public AzureAIService(AppSettings settings)
    {
        _settings = settings.AzureAI;

        _client = !string.IsNullOrWhiteSpace(_settings.ApiKey)
            ? new AzureOpenAIClient(new Uri(_settings.Endpoint), new AzureKeyCredential(_settings.ApiKey))
            : new AzureOpenAIClient(new Uri(_settings.Endpoint), new DefaultAzureCredential());
    }

    public async Task<CompletionResult> CompleteAsync(
        string systemPrompt,
        string userMessage,
        bool useReasoningModel = true,
        CancellationToken cancellationToken = default)
    {
        _ = useReasoningModel;
        var chatClient = _client.GetChatClient(_settings.ChatModel);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userMessage)
        };

        var response = await chatClient.CompleteChatAsync(messages, cancellationToken: cancellationToken);
        var text = response.Value.Content[0].Text;
        var usage = response.Value.Usage;

        return new CompletionResult(
            text,
            usage?.InputTokenCount ?? 0,
            usage?.OutputTokenCount ?? 0,
            usage?.TotalTokenCount ?? 0);
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
}

/// <summary>Chat completion result with token usage.</summary>
public record CompletionResult(string Text, int InputTokens, int OutputTokens, int TotalTokens);
