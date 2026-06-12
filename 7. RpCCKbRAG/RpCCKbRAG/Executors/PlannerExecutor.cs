using Microsoft.Agents.AI.Workflows;
using RpCCKbRAG.Models;
using RpCCKbRAG.Services;

namespace RpCCKbRAG.Executors;

/// <summary>
/// Node 1 — Planner
///
/// Decomposes the user's question into a minimal, ordered research plan.
/// Vector-only build: every step uses the single tool `search_docs`, which
/// queries the in-memory KB built from the Contoso PDF corpus.
/// </summary>
[SendsMessage(typeof(StepSignal))]
public sealed class PlannerExecutor : Executor<UserQuery>
{
    private readonly AzureAIService     _ai;
    private readonly KnowledgeBaseState _kbState;

    internal const string StateScope = "rag";
    internal const string StateKey   = "state";

    public PlannerExecutor(AzureAIService ai, KnowledgeBaseState kbState) : base("Planner")
    {
        _ai      = ai;
        _kbState = kbState;
    }

    public override async ValueTask HandleAsync(
        UserQuery message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine("\n" + new string('─', 80));
        Console.WriteLine("[Planner] Decomposing query into a research plan...");
        Console.WriteLine($"  Query: {message.Query}");

        var systemPrompt = $$"""
            You are a strategic research planning agent for a Contoso call-center
            knowledge-base assistant.

            Available tool:
              • search_docs — searches the internal knowledge base which contains:
                {{_kbState.Description}}

            Task: Decompose the user's question into a minimal, ordered set of sub-questions
            that can each be answered by `search_docs` against the knowledge base.

            Rules:
            - Every step MUST use tool "search_docs" (this build has no other retrieval tool).
            - Keep the plan to 2–5 steps; avoid redundant or trivially overlapping steps.
            - Sequence steps so earlier findings can refine later queries.
            - If the question is simple, a single step is fine.

            Return JSON exactly:
            {
              "steps": [
                { "subQuestion": "...", "reasoning": "...", "tool": "search_docs" }
              ]
            }
            """;

        var plan = await _ai.CompleteStructuredAsync<ResearchPlan>(
            systemPrompt,
            $"Create a research plan for:\n\n{message.Query}",
            useReasoningModel: true,
            cancellationToken);

        var steps = plan?.Steps ?? new List<ResearchStep>();
        if (steps.Count == 0)
        {
            steps.Add(new ResearchStep
            {
                SubQuestion = message.Query,
                Reasoning   = "Direct fallback — no plan was generated.",
                Tool        = "search_docs"
            });
        }

        // Defensive: force every step to search_docs in this vector-only build.
        foreach (var step in steps) step.Tool = "search_docs";

        Console.WriteLine($"[Planner] {steps.Count}-step plan created:");
        for (int i = 0; i < steps.Count; i++)
            Console.WriteLine($"  {i + 1}. [{steps[i].Tool,-12}] {steps[i].SubQuestion}");

        var state = new RagState
        {
            UserQuery        = message.Query,
            Plan             = steps,
            CurrentStepIndex = 0,
            IterationCount   = 0
        };
        await context.QueueStateUpdateAsync(StateKey, state, scopeName: StateScope, cancellationToken);

        await context.SendMessageAsync(new StepSignal(0), cancellationToken: cancellationToken);
    }
}
