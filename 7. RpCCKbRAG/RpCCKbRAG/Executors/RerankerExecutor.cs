using Microsoft.Agents.AI.Workflows;
using RpCCKbRAG.Models;
using RpCCKbRAG.Services;
using System.Text.Json.Serialization;

namespace RpCCKbRAG.Executors;

/// <summary>
/// Node 4 — Cross-Encoder Reranker.
/// Uses the fast LLM as a cross-encoder proxy: scores each document's relevance
/// to the sub-question and returns only the top-K most relevant ones.
/// </summary>
[SendsMessage(typeof(RankedResults))]
public sealed class RerankerExecutor : Executor<SearchResults>
{
    private readonly AzureAIService _ai;
    private readonly int            _topK;

    public RerankerExecutor(AzureAIService ai, int topK = 3) : base("Reranker")
    {
        _ai   = ai;
        _topK = topK;
    }

    public override async ValueTask HandleAsync(
        SearchResults message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"\n[Reranker] Reranking {message.Documents.Count} documents → top {_topK}...");

        if (message.Documents.Count == 0)
        {
            await context.SendMessageAsync(
                new RankedResults(new List<RagDocument>(), message.SubQuestion, message.StepIndex),
                cancellationToken: cancellationToken);
            return;
        }

        const int PreviewLength = 400;
        var docList = string.Join("\n\n", message.Documents.Select((d, i) =>
        {
            var preview = d.Content.Length > PreviewLength
                ? d.Content[..PreviewLength] + "…"
                : d.Content;
            return $"[{i}] Source: {d.Source} | Section: {d.Section}\n{preview}";
        }));

        const string SystemPrompt = """
            You are a precision relevance reranker.

            Given a question and numbered document excerpts, return the indices of the
            most relevant documents in descending relevance order.

            Return JSON exactly:
            { "ranked_indices": [2, 0, 5] }

            Include only documents that genuinely help answer the question.
            """;

        var response = await _ai.CompleteStructuredAsync<RankerResponse>(
            SystemPrompt,
            $"Question: {message.SubQuestion}\n\nDocuments:\n{docList}\n\nReturn the {_topK} most relevant indices:",
            useReasoningModel: false,
            cancellationToken);

        var indices = response?.RankedIndices
            ?? Enumerable.Range(0, Math.Min(_topK, message.Documents.Count)).ToList();

        var topDocs = indices
            .Where(i => i >= 0 && i < message.Documents.Count)
            .Distinct()
            .Take(_topK)
            .Select((i, rank) => message.Documents[i] with { RelevanceScore = 1f / (rank + 1) })
            .ToList();

        Console.WriteLine($"  Kept {topDocs.Count} documents after reranking.");

        await context.SendMessageAsync(
            new RankedResults(topDocs, message.SubQuestion, message.StepIndex),
            cancellationToken: cancellationToken);
    }

    private sealed class RankerResponse
    {
        [JsonPropertyName("ranked_indices")]
        public List<int>? RankedIndices { get; set; }
    }
}
