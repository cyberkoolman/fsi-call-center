using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using RpCCKbRAG.Configuration;
using RpCCKbRAG.Models;
using RpCCKbRAG.Services;
using RpCCKbRAG.Workflow;
using System.Runtime.CompilerServices;

namespace RpCCKbRAG.Api;

/// <summary>
/// IChatClient that wraps a RAG pipeline (agentic or one-shot). The workflow
/// is built once at startup against the shared (already-populated) VectorStore;
/// every chat turn submits the latest user message into the workflow and
/// streams the synthesised answer back to DevUI.
/// </summary>
public sealed class RagWorkflowChatClient : IChatClient
{
    private readonly bool _useOneShot;
    private readonly Microsoft.Agents.AI.Workflows.Workflow _workflow;

    public RagWorkflowChatClient(
        AppSettings        settings,
        AzureAIService     ai,
        VectorStore        store,
        KnowledgeBaseState kbState,
        bool               useOneShot)
    {
        _useOneShot = useOneShot;
        _workflow   = useOneShot
            ? OneShotRagWorkflow.Build(ai, store, kbState, settings.Pipeline)
            : AgenticRagWorkflow.Build(ai, store, kbState, settings.Pipeline);
    }

    public ChatClientMetadata Metadata => new(
        _useOneShot ? "OneShotRAG" : "AgenticRAG",
        providerUri: null,
        defaultModelId: _useOneShot ? "oneshot-rag-pipeline" : "agentic-rag-pipeline");

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var chunks = new List<string>();
        await foreach (var update in GetStreamingResponseAsync(chatMessages, options, cancellationToken))
            if (update.Text is { } t) chunks.Add(t);

        return new ChatResponse(new ChatMessage(ChatRole.Assistant, string.Concat(chunks)));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var userText = chatMessages
            .LastOrDefault(m => m.Role == ChatRole.User)?.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(userText))
        {
            yield return Text("Please ask a question about the Contoso knowledge base.");
            yield break;
        }

        await foreach (var u in RunPipelineAsync(userText, ct))
            yield return u;
    }

    public object? GetService(Type serviceType, object? key = null) => null;

    public void Dispose() { }

    // ── Pipeline execution ─────────────────────────────────────────────────

    private async IAsyncEnumerable<ChatResponseUpdate> RunPipelineAsync(
        string query,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var label = _useOneShot ? "One-Shot" : "Deep-Thinking";
        Console.WriteLine($"\n[RAG] Query ({label}): {query}");

        string? answer = null;
        await using var run = await InProcessExecution.RunStreamingAsync(_workflow, new UserQuery(query));

        await foreach (var evt in run.WatchStreamAsync())
        {
            if (evt is WorkflowOutputEvent outputEvent)
            {
                answer = outputEvent.Data as string;
                break;
            }
        }

        answer ??= "The research pipeline did not produce an answer.";

        // Stream the final answer to DevUI sentence-by-sentence so the user
        // sees progressive output rather than a single late blob.
        var sentences = answer.Split([". ", ".\n"], StringSplitOptions.None);
        for (int i = 0; i < sentences.Length; i++)
        {
            var chunk = i < sentences.Length - 1 ? sentences[i] + ". " : sentences[i];
            if (string.IsNullOrWhiteSpace(chunk)) continue;
            yield return new ChatResponseUpdate
            {
                Role = ChatRole.Assistant,
                Contents = [new TextContent(chunk)]
            };
            await Task.Delay(25, ct);
        }
    }

    private static ChatResponseUpdate Text(string text) => new()
    {
        Role = ChatRole.Assistant,
        Contents = [new TextContent(text)]
    };
}
