using Microsoft.Agents.AI.Workflows;
using RpCCKbRAG.Models;
using RpCCKbRAG.Services;

namespace RpCCKbRAG.Executors;

/// <summary>
/// Node 8 — Synthesis Agent (terminal).
/// Reads the complete research history and distilled contexts and produces a
/// single comprehensive, citable answer for the original user query.
/// </summary>
[YieldsOutput(typeof(string))]
public sealed class SynthesisExecutor : Executor<FinishSignal>
{
    private readonly AzureAIService _ai;

    public SynthesisExecutor(AzureAIService ai) : base("Synthesis") => _ai = ai;

    public override async ValueTask HandleAsync(
        FinishSignal message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var state = await context.ReadStateAsync<RagState>(
                        PlannerExecutor.StateKey,
                        scopeName: PlannerExecutor.StateScope,
                        cancellationToken)
                    ?? throw new InvalidOperationException("RAG state not found.");

        Console.WriteLine("\n" + new string('─', 80));
        Console.WriteLine("[Synthesis] Generating final comprehensive answer...");
        Console.WriteLine($"  Research steps completed : {state.ResearchHistory.Count}");

        var evidenceBlock = state.DistilledContexts.Count > 0
            ? string.Join("\n\n", state.DistilledContexts)
            : string.Join("\n", state.ResearchHistory);

        var historySummary = string.Join("\n", state.ResearchHistory);

        const string SystemPrompt = """
            You are a senior call-center knowledge synthesis agent.

            Your task: write a comprehensive, well-structured answer to the original
            question by integrating all research findings drawn from the Contoso
            knowledge base.

            Guidelines:
            1. Perform multi-hop reasoning — explicitly connect facts across sources.
            2. Cite each fact inline using the format [Source — Section] (e.g.
               "[Tech Support Guidelines — page 4]") drawn from the evidence below.
            3. Structure the answer with short headings or a numbered list when complexity warrants.
            4. Be precise: include specific policy names, escalation paths, and named roles.
            5. Address every aspect of the original question.
            6. End with a one-sentence synthesis that connects the findings.
            7. Acknowledge clearly when information is not in the knowledge base.
            """;

        var finalAnswer = await _ai.CompleteAsync(
            SystemPrompt,
            $"Original query:\n{state.UserQuery}\n\n" +
            $"Research summary:\n{historySummary}\n\n" +
            $"Detailed evidence:\n{evidenceBlock}\n\n" +
            "Write the comprehensive answer:",
            useReasoningModel: true,
            cancellationToken);

        Console.WriteLine("\n" + new string('═', 80));
        Console.WriteLine("FINAL ANSWER");
        Console.WriteLine(new string('═', 80));
        Console.WriteLine(finalAnswer);
        Console.WriteLine(new string('═', 80));

        await context.YieldOutputAsync(finalAnswer, cancellationToken);
    }
}
