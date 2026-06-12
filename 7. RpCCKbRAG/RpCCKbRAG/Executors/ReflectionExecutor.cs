using Microsoft.Agents.AI.Workflows;
using RpCCKbRAG.Models;
using RpCCKbRAG.Services;

namespace RpCCKbRAG.Executors;

/// <summary>
/// Node 6 — Reflection Agent.
/// Reads the distilled evidence, produces a one-sentence summary of what was
/// learned, and appends it to the cumulative research history.
/// </summary>
[SendsMessage(typeof(PolicySignal))]
public sealed class ReflectionExecutor : Executor<DistilledContext>
{
    private readonly AzureAIService _ai;

    public ReflectionExecutor(AzureAIService ai) : base("Reflection") => _ai = ai;

    public override async ValueTask HandleAsync(
        DistilledContext message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var state = await context.ReadStateAsync<RagState>(
                        PlannerExecutor.StateKey,
                        scopeName: PlannerExecutor.StateScope,
                        cancellationToken)
                    ?? throw new InvalidOperationException("RAG state not found.");

        Console.WriteLine($"\n[Reflection] Reflecting on Step {message.StepIndex + 1} findings...");

        const string SystemPrompt = """
            You are a research reflection agent.

            Summarise the key finding from the retrieved context in exactly one concise sentence.
            Be specific: include concrete facts, policy names, or named entities where present.
            The sentence will be added to a research log read by a policy agent.
            """;

        var summary = await _ai.CompleteAsync(
            SystemPrompt,
            $"Original query  : {state.UserQuery}\n" +
            $"Sub-question    : {message.SubQuestion}\n" +
            $"Retrieved context:\n{message.Context}",
            useReasoningModel: false,
            cancellationToken);

        summary = summary.Trim().TrimEnd('.');

        var stepLabel = $"Step {message.StepIndex + 1} [{state.Plan[message.StepIndex].Tool}]: {summary}.";
        state.ResearchHistory.Add(stepLabel);
        state.DistilledContexts.Add(
            $"=== Step {message.StepIndex + 1}: {message.SubQuestion} ===\n{message.Context}");

        Console.WriteLine($"  → {stepLabel}");

        await context.QueueStateUpdateAsync(
            PlannerExecutor.StateKey, state,
            scopeName: PlannerExecutor.StateScope,
            cancellationToken);

        await context.SendMessageAsync(
            new PolicySignal(message.StepIndex),
            cancellationToken: cancellationToken);
    }
}
