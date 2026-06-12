using Microsoft.Agents.AI.Workflows;
using RpCCKbRAG.Models;
using RpCCKbRAG.Services;

namespace RpCCKbRAG.Executors;

/// <summary>
/// Node 2 — Query Rewriter
///
/// First iteration with no prior history → passes the original user query through
/// unchanged (matches the one-shot pipeline's starting retrieval). Subsequent
/// iterations rewrite the sub-question incorporating prior findings so the
/// query targets what is still unknown.
/// </summary>
[SendsMessage(typeof(SearchRequest))]
public sealed class QueryRewriterExecutor : Executor<StepSignal>
{
    private readonly AzureAIService _ai;

    public QueryRewriterExecutor(AzureAIService ai) : base("QueryRewriter") => _ai = ai;

    public override async ValueTask HandleAsync(
        StepSignal message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var state = await context.ReadStateAsync<RagState>(
                        PlannerExecutor.StateKey,
                        scopeName: PlannerExecutor.StateScope,
                        cancellationToken)
                    ?? throw new InvalidOperationException("RAG state not found in workflow context.");

        state.CurrentStepIndex = message.StepIndex;
        state.IterationCount++;

        if (message.StepIndex >= state.Plan.Count)
        {
            Console.WriteLine("[QueryRewriter] Step index out of range — skipping.");
            return;
        }

        var step = state.Plan[message.StepIndex];
        Console.WriteLine("\n" + new string('─', 80));
        Console.WriteLine($"[QueryRewriter] Step {message.StepIndex + 1}/{state.Plan.Count}");
        Console.WriteLine($"  Sub-question : {step.SubQuestion}");
        Console.WriteLine($"  Tool         : {step.Tool}");

        var historyContext = state.ResearchHistory.Count > 0
            ? $"\n\nPrevious findings:\n{string.Join("\n", state.ResearchHistory)}"
            : string.Empty;

        string rewritten;

        if (state.ResearchHistory.Count == 0)
        {
            rewritten = state.UserQuery;
            Console.WriteLine($"  Rewritten    : {rewritten}  [passthrough — original query]");
        }
        else
        {
            const string SystemPrompt = """
                You are a search query optimisation specialist for a knowledge-base
                vector search engine.

                Rewrite the given sub-question into the single most effective search query.
                Incorporate key findings from previous research steps to focus the query
                on what is still unknown. Keep natural language phrasing for conceptual
                questions; use precise keyword form only for exact section titles or
                policy names.

                Return ONLY the rewritten query string — no explanation, no punctuation wrappers.
                """;

            rewritten = await _ai.CompleteAsync(
                SystemPrompt,
                $"Sub-question : {step.SubQuestion}{historyContext}\n\nRewritten query:",
                useReasoningModel: false,
                cancellationToken);

            rewritten = rewritten.Trim().Trim('"').Trim('\'');
            Console.WriteLine($"  Rewritten    : {rewritten}");
        }

        await context.QueueStateUpdateAsync(
            PlannerExecutor.StateKey, state,
            scopeName: PlannerExecutor.StateScope,
            cancellationToken);

        await context.SendMessageAsync(
            new SearchRequest(rewritten, step.Tool, step.SubQuestion, message.StepIndex),
            cancellationToken: cancellationToken);
    }
}
