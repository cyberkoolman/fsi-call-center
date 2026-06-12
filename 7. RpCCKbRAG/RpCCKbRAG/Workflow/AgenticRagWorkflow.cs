using Microsoft.Agents.AI.Workflows;
using RpCCKbRAG.Configuration;
using RpCCKbRAG.Executors;
using RpCCKbRAG.Models;
using RpCCKbRAG.Services;

namespace RpCCKbRAG.Workflow;

/// <summary>
/// Assembles the Deep-Thinking RAG workflow graph (vector-only build).
///
/// Graph topology (edges):
///
///   UserQuery
///       │
///   [Planner]  ──────────────────────────────────────────────────────┐
///       │ StepSignal(0)                                              │
///       ▼                                                            │
///   [QueryRewriter] ◄──── StepSignal(n+1) ─── [Policy] ◄─────────────┤
///       │ SearchRequest                           │ FinishSignal     │
///       ▼                                         ▼                  │
///   [VectorSearch]                          [Synthesis]              │
///       │ SearchResults                          │ yield output      │
///       ▼                                        ▼                   │
///   [Reranker]                              workflow END             │
///       │ RankedResults                                              │
///       ▼                                                            │
///   [Distiller]                                                      │
///       │ DistilledContext                                           │
///       ▼                                                            │
///   [Reflection] ──────── PolicySignal ──────────────────────────────┘
/// </summary>
public static class AgenticRagWorkflow
{
    public static Microsoft.Agents.AI.Workflows.Workflow Build(
        AzureAIService     aiService,
        VectorStore        vectorStore,
        KnowledgeBaseState kbState,
        PipelineSettings   pipelineSettings,
        string             name = "")
    {
        var planner       = new PlannerExecutor(aiService, kbState);
        var queryRewriter = new QueryRewriterExecutor(aiService);
        var vectorSearch  = new VectorSearchExecutor(aiService, vectorStore, pipelineSettings.InitialRetrievalTopK);
        var reranker      = new RerankerExecutor(aiService, pipelineSettings.RerankerTopK);
        var distiller     = new DistillerExecutor(aiService);
        var reflection    = new ReflectionExecutor(aiService);
        var policy        = new PolicyExecutor(aiService);
        var synthesis     = new SynthesisExecutor(aiService);

        var workflowBuilder = new WorkflowBuilder(planner)
            .AddEdge(planner, queryRewriter)
            .AddEdge(queryRewriter, vectorSearch)
            .AddEdge(vectorSearch, reranker)
            .AddEdge(reranker, distiller)
            .AddEdge(distiller, reflection)
            .AddEdge(reflection, policy)
            // Policy CONTINUE → loop back to QueryRewriter
            .AddEdge<StepSignal>(policy, queryRewriter, condition: _ => true)
            // Policy FINISH → Synthesis (terminal)
            .AddEdge<FinishSignal>(policy, synthesis, condition: _ => true)
            .WithOutputFrom(synthesis);

        if (!string.IsNullOrEmpty(name))
            workflowBuilder = workflowBuilder.WithName(name);

        return workflowBuilder.Build();
    }
}
