using Microsoft.Agents.AI.Workflows;
using RpCCKbRAG.Configuration;
using RpCCKbRAG.Executors;
using RpCCKbRAG.Services;

namespace RpCCKbRAG.Workflow;

/// <summary>
/// The simplest possible RAG workflow:
///
///   UserQuery ─► [VectorSearch] ─► [Answer]  ─► output
///
/// One vector lookup, one LLM call, citations included.
/// </summary>
public static class ContosoKbWorkflow
{
    public const string Name = "ContosoKB";

    public static Microsoft.Agents.AI.Workflows.Workflow Build(
        AzureAIService   aiService,
        VectorStore      vectorStore,
        PipelineSettings pipeline)
    {
        var search = new VectorSearchExecutor(aiService, vectorStore, pipeline.TopK);
        var answer = new AnswerExecutor(aiService);

        return new WorkflowBuilder(search)
            .AddEdge(search, answer)
            .WithOutputFrom(answer)
            .WithName(Name)
            .Build();
    }
}
