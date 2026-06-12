using Microsoft.Agents.AI.Workflows;
using RpCCKbRAG.Models;
using RpCCKbRAG.Services;

namespace RpCCKbRAG.Executors;

/// <summary>
/// Node 5 — Contextual Distiller.
/// Compresses the top-K reranked documents into a single dense paragraph of
/// evidence, removing redundancy and preserving citations.
/// </summary>
[SendsMessage(typeof(DistilledContext))]
public sealed class DistillerExecutor : Executor<RankedResults>
{
    private readonly AzureAIService _ai;

    public DistillerExecutor(AzureAIService ai) : base("Distiller") => _ai = ai;

    public override async ValueTask HandleAsync(
        RankedResults message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"\n[Distiller] Distilling {message.TopDocuments.Count} documents...");

        if (message.TopDocuments.Count == 0)
        {
            Console.WriteLine("  No documents to distill — returning empty context.");
            await context.SendMessageAsync(
                new DistilledContext("No relevant information found.", message.SubQuestion, message.StepIndex),
                cancellationToken: cancellationToken);
            return;
        }

        var docs = string.Join("\n\n---\n\n", message.TopDocuments.Select(d =>
            $"Source : {d.Source}\nSection: {d.Section}\n\n{d.Content}"));

        const string SystemPrompt = """
            You are a contextual distillation agent.

            Task: Synthesise the provided document excerpts into a single, dense paragraph
            that captures all information needed to answer the question.

            Rules:
            - Preserve exact policy names, numbers, dates, and proper nouns.
            - Cite each fact inline using the format [Source — Section] (e.g.
              "[Tech Support Guidelines — page 4]") drawn directly from the
              "Source" and "Section" fields above.
            - Remove redundancy; never repeat the same fact twice.
            - Do NOT speculate or add information not present in the documents.
            - Keep the distilled paragraph under 300 words.
            """;

        var distilled = await _ai.CompleteAsync(
            SystemPrompt,
            $"Question: {message.SubQuestion}\n\nDocuments:\n{docs}\n\nDistilled context:",
            useReasoningModel: false,
            cancellationToken);

        distilled = distilled.Trim();
        Console.WriteLine($"  Distilled to {distilled.Length} characters.");

        await context.SendMessageAsync(
            new DistilledContext(distilled, message.SubQuestion, message.StepIndex),
            cancellationToken: cancellationToken);
    }
}
