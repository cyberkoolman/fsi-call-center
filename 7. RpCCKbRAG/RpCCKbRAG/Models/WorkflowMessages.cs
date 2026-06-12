namespace RpCCKbRAG.Models;

// ─────────────────────────────────────────────────────────────────────────────
// Workflow message types — each record is the typed payload flowing over an edge
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Initial trigger sent to PlannerExecutor.</summary>
public record UserQuery(string Query);

/// <summary>
/// Sent by Planner (step 0) and Policy (subsequent steps) to QueryRewriterExecutor.
/// Carries the index of the step to process next — enables the feedback loop.
/// </summary>
public record StepSignal(int StepIndex);

/// <summary>
/// Sent by QueryRewriter to VectorSearch. In this vector-only build the
/// Tool field is always "search_docs"; it is preserved to keep the message
/// shape compatible with the upstream Agentic.RAG reference.
/// </summary>
public record SearchRequest(
    string RewrittenQuery,
    string Tool,
    string OriginalSubQuestion,
    int StepIndex);

public record SearchResults(
    List<RagDocument> Documents,
    string SubQuestion,
    int StepIndex);

public record RankedResults(
    List<RagDocument> TopDocuments,
    string SubQuestion,
    int StepIndex);

public record DistilledContext(
    string Context,
    string SubQuestion,
    int StepIndex);

public record PolicySignal(int CompletedStepIndex);

public record FinishSignal();
