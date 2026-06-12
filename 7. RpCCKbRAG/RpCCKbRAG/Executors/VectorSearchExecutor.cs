using Microsoft.Agents.AI.Workflows;
using RpCCKbRAG.Models;
using RpCCKbRAG.Services;

namespace RpCCKbRAG.Executors;

/// <summary>
/// Embeds the user's query and pulls the top-K most similar chunks from the
/// in-memory VectorStore. Pure vector similarity — no strategy selection,
/// no rewriting, no LLM call.
/// </summary>
[SendsMessage(typeof(RetrievedContext))]
public sealed class VectorSearchExecutor : Executor<UserQuery>
{
    private readonly AzureAIService _ai;
    private readonly VectorStore    _store;
    private readonly int            _topK;

    public VectorSearchExecutor(AzureAIService ai, VectorStore store, int topK = 5)
        : base("VectorSearch")
    {
        _ai    = ai;
        _store = store;
        _topK  = topK;
    }

    public override async ValueTask HandleAsync(
        UserQuery message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"\n[VectorSearch] Query: {message.Query}");

        var embedding = await _ai.GenerateEmbeddingAsync(message.Query, cancellationToken);
        var docs      = _store.VectorSearch(embedding, _topK);

        Console.WriteLine($"  Retrieved {docs.Count} chunks (top-{_topK})");

        await context.SendMessageAsync(
            new RetrievedContext(message.Query, docs),
            cancellationToken: cancellationToken);
    }
}
