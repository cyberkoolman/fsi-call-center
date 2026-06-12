using Microsoft.Agents.AI.Workflows;
using RpCCKbRAG.Models;
using RpCCKbRAG.Services;

namespace RpCCKbRAG.Executors;

/// <summary>
/// Node 3 — Vector / Keyword / Hybrid Search (internal knowledge base)
///
/// Internally chooses the best retrieval strategy (vector, keyword, or hybrid)
/// for the rewritten query, executes it against the in-memory VectorStore, and
/// passes the broad candidate set on to the Reranker.
/// </summary>
[SendsMessage(typeof(SearchResults))]
public sealed class VectorSearchExecutor : Executor<SearchRequest>
{
    private readonly AzureAIService _ai;
    private readonly VectorStore    _store;
    private readonly int            _topK;

    public VectorSearchExecutor(AzureAIService ai, VectorStore store, int topK = 10)
        : base("VectorSearch")
    {
        _ai    = ai;
        _store = store;
        _topK  = topK;
    }

    public override async ValueTask HandleAsync(
        SearchRequest message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"\n[VectorSearch] Searching knowledge base index...");
        Console.WriteLine($"  Query : {message.RewrittenQuery}");

        var strategy = await ChooseStrategyAsync(message.RewrittenQuery, cancellationToken);
        Console.WriteLine($"  Strategy selected: {strategy}");

        List<RagDocument> results;

        if (strategy == SearchStrategy.Vector || strategy == SearchStrategy.Hybrid)
        {
            var embedding = await _ai.GenerateEmbeddingAsync(message.RewrittenQuery, cancellationToken);
            results = strategy == SearchStrategy.Hybrid
                ? _store.HybridSearch(message.RewrittenQuery, embedding, _topK)
                : _store.VectorSearch(embedding, _topK);
        }
        else
        {
            results = _store.KeywordSearch(message.RewrittenQuery, _topK);
        }

        Console.WriteLine($"  Retrieved {results.Count} candidate documents.");

        await context.SendMessageAsync(
            new SearchResults(results, message.OriginalSubQuestion, message.StepIndex),
            cancellationToken: cancellationToken);
    }

    private async Task<SearchStrategy> ChooseStrategyAsync(string query, CancellationToken ct)
    {
        const string SystemPrompt = """
            You are a retrieval strategy supervisor for a document search engine.

            Choose the single best search strategy for the query:
              • vector   — open-ended conceptual, thematic, or analytical questions where meaning
                           matters more than exact words
              • keyword  — queries that are PRIMARILY composed of exact identifiers with no
                           analytical component: specific product model numbers, version strings,
                           policy names, exact quoted phrases, or precise section titles
              • hybrid   — queries that mix a conceptual question with specific named entities
                           or domain terms

            Note: an entity or subject name alone is NOT sufficient to choose keyword.
            Default to hybrid when in doubt.

            Respond with exactly one word: vector, keyword, or hybrid.
            """;

        var response = await _ai.CompleteAsync(SystemPrompt, $"Query: {query}", useReasoningModel: false, ct);

        return response.Trim().ToLowerInvariant() switch
        {
            "keyword" => SearchStrategy.Keyword,
            "vector"  => SearchStrategy.Vector,
            _         => SearchStrategy.Hybrid
        };
    }
}
