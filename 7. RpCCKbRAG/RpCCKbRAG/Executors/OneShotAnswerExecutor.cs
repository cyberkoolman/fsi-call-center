using Microsoft.Agents.AI.Workflows;
using RpCCKbRAG.Models;
using RpCCKbRAG.Services;

namespace RpCCKbRAG.Executors;

/// <summary>
/// One-Shot RAG — Terminal answer node.
///
/// Receives the reranked top-K documents and generates a final answer in a single
/// LLM call. No distillation, no reflection, no policy loop — one pass and done.
/// </summary>
[YieldsOutput(typeof(string))]
public sealed class OneShotAnswerExecutor : Executor<RankedResults>
{
    private readonly AzureAIService _ai;

    public OneShotAnswerExecutor(AzureAIService ai) : base("OneShotAnswer") => _ai = ai;

    public override async ValueTask HandleAsync(
        RankedResults message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine("\n" + new string('─', 80));
        Console.WriteLine("[OneShotAnswer] Generating answer from reranked documents (single pass)...");
        Console.WriteLine($"  Documents available: {message.TopDocuments.Count}");

        if (message.TopDocuments.Count == 0)
        {
            const string fallback = "I could not find relevant information in the knowledge base to answer your question.";
            Console.WriteLine($"  → {fallback}");
            await context.YieldOutputAsync(fallback, cancellationToken);
            return;
        }

        var docs = string.Join("\n\n---\n\n", message.TopDocuments.Select(d =>
            $"Source : {d.Source}\nSection: {d.Section}\n\n{d.Content}"));

        const string SystemPrompt = """
            You are a helpful Contoso call-center knowledge-base assistant.

            Answer the user's question using ONLY the provided documents.
            - Cite sources inline using the format [Source — Section]
              (e.g. "[Tech Support Guidelines — page 4]") drawn from the
              "Source" and "Section" fields above.
            - Be precise: include specific policy names, escalation paths, and named roles.
            - If the documents do not contain enough information, say so clearly.
            - Do NOT speculate or add information beyond what the documents provide.
            """;

        var answer = await _ai.CompleteAsync(
            SystemPrompt,
            $"Question: {message.SubQuestion}\n\nDocuments:\n{docs}\n\nAnswer:",
            useReasoningModel: true,
            cancellationToken);

        Console.WriteLine("\n" + new string('═', 80));
        Console.WriteLine("FINAL ANSWER (One-Shot)");
        Console.WriteLine(new string('═', 80));
        Console.WriteLine(answer);
        Console.WriteLine(new string('═', 80));

        await context.YieldOutputAsync(answer, cancellationToken);
    }
}
