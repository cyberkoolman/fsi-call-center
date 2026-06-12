using Microsoft.Agents.AI.Workflows;
using RpCCKbRAG.Models;
using RpCCKbRAG.Services;

namespace RpCCKbRAG.Executors;

/// <summary>
/// Terminal executor — formats the retrieved chunks into a prompt and emits the
/// LLM's grounded answer as the workflow output.
/// </summary>
[YieldsOutput(typeof(string))]
public sealed class AnswerExecutor : Executor<RetrievedContext>
{
    private readonly AzureAIService _ai;

    public AnswerExecutor(AzureAIService ai) : base("Answer") => _ai = ai;

    public override async ValueTask HandleAsync(
        RetrievedContext message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine("[Answer] Generating answer from retrieved chunks...");

        if (message.Documents.Count == 0)
        {
            const string fallback = "I could not find relevant information in the Contoso knowledge base to answer that question.";
            await context.YieldOutputAsync(fallback, cancellationToken);
            return;
        }

        var docs = string.Join("\n\n---\n\n", message.Documents.Select(d =>
            $"Source : {d.Source}\nSection: {d.Section}\n\n{d.Content}"));

        const string SystemPrompt = """
            You are a helpful Contoso call-center knowledge-base assistant.

            Answer the user's question using ONLY the provided documents.
            - Cite sources inline using the format [Source — Section]
              (e.g. "[Contoso Tech Support Guidelines — page 4]") drawn from
              the "Source" and "Section" fields above.
            - Be precise: include specific policy names, escalation paths,
              and named roles when they appear in the documents.
            - If the documents do not contain enough information to answer,
              say so clearly. Do NOT speculate or add outside knowledge.
            """;

        var answer = await _ai.CompleteAsync(
            SystemPrompt,
            $"Question: {message.Query}\n\nDocuments:\n{docs}\n\nAnswer:",
            useReasoningModel: true,
            cancellationToken);

        Console.WriteLine($"[Answer] {answer.Length} chars produced.");
        await context.YieldOutputAsync(answer, cancellationToken);
    }
}
