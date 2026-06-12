using Microsoft.Agents.AI.Workflows;
using RpCCKbRAG.Configuration;
using RpCCKbRAG.Executors;
using RpCCKbRAG.Models;
using RpCCKbRAG.Services;

namespace RpCCKbRAG.Workflow;

/// <summary>
/// Assembles the One-Shot RAG workflow — a simple linear pipeline for
/// comparison with the deep-thinking agentic workflow.
///
/// Graph topology (no loops):
///
///   UserQuery
///       │
///   [QueryBridge]    — wraps the raw query as a SearchRequest (no LLM)
///       │ SearchRequest
///   [VectorSearch]   — strategy supervisor + broad recall (top-K)
///       │ SearchResults
///   [Reranker]       — precision filter (top-K)
///       │ RankedResults
///   [OneShotAnswer]  — single LLM call → yield output
///
/// What is intentionally missing (to contrast with the agentic pipeline):
///   • No Planner          — query is not decomposed into sub-questions
///   • No QueryRewriter    — raw query goes directly to search
///   • No Distiller        — raw chunks go to the LLM
///   • No Reflection       — no cumulative research memory
///   • No Policy           — no continue/re-think/finish loop
///   • No Synthesis        — no multi-step evidence integration
/// </summary>
public static class OneShotRagWorkflow
{
    public static Microsoft.Agents.AI.Workflows.Workflow Build(
        AzureAIService     aiService,
        VectorStore        vectorStore,
        KnowledgeBaseState kbState,
        PipelineSettings   pipelineSettings,
        string             name = "")
    {
        var queryBridge  = new QueryBridgeExecutor();
        var vectorSearch = new VectorSearchExecutor(aiService, vectorStore, pipelineSettings.InitialRetrievalTopK);
        var reranker     = new RerankerExecutor(aiService, pipelineSettings.RerankerTopK);
        var answer       = new OneShotAnswerExecutor(aiService);

        var workflowBuilder = new WorkflowBuilder(queryBridge)
            .AddEdge(queryBridge, vectorSearch)
            .AddEdge(vectorSearch, reranker)
            .AddEdge(reranker, answer)
            .WithOutputFrom(answer);

        if (!string.IsNullOrEmpty(name))
            workflowBuilder = workflowBuilder.WithName(name);

        return workflowBuilder.Build();
    }

    /// <summary>
    /// Converts a UserQuery into a SearchRequest with the raw query text.
    /// No LLM call, no rewriting — intentionally naive.
    /// </summary>
    [SendsMessage(typeof(SearchRequest))]
    private sealed class QueryBridgeExecutor : Executor<UserQuery>
    {
        public QueryBridgeExecutor() : base("QueryBridge") { }

        public override ValueTask HandleAsync(
            UserQuery message,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            Console.WriteLine($"\n[QueryBridge] Passing raw query to vector search (no rewriting)");
            Console.WriteLine($"  Query: {message.Query}");

            return context.SendMessageAsync(
                new SearchRequest(
                    RewrittenQuery:      message.Query,
                    Tool:                "search_docs",
                    OriginalSubQuestion: message.Query,
                    StepIndex:           0),
                cancellationToken: cancellationToken);
        }
    }
}
