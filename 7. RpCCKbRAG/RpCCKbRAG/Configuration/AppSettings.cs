namespace RpCCKbRAG.Configuration;

public class AppSettings
{
    public AzureAISettings AzureAI { get; set; } = new();
    public PipelineSettings Pipeline { get; set; } = new();
    public KnowledgeSettings Knowledge { get; set; } = new();
}

public class AzureAISettings
{
    /// <summary>Azure AI Foundry or Azure OpenAI endpoint URL.</summary>
    public string Endpoint { get; set; } = "";

    /// <summary>API key for the endpoint. Leave empty to use DefaultAzureCredential.</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>Deployment name for the high-capability model (planning, policy, synthesis).</summary>
    public string ReasoningModel { get; set; } = "gpt-4o";

    /// <summary>Deployment name for the fast model (rewriting, reranking, distillation, reflection).</summary>
    public string FastModel { get; set; } = "gpt-4o-mini";

    /// <summary>Deployment name for the text embedding model.</summary>
    public string EmbeddingModel { get; set; } = "text-embedding-3-large";
}

public class PipelineSettings
{
    /// <summary>Approximate token size for each document chunk.</summary>
    public int ChunkSize { get; set; } = 500;

    /// <summary>Token overlap between consecutive chunks.</summary>
    public int ChunkOverlap { get; set; } = 50;

    /// <summary>Number of documents to retrieve before reranking.</summary>
    public int InitialRetrievalTopK { get; set; } = 10;

    /// <summary>Number of documents to keep after reranking.</summary>
    public int RerankerTopK { get; set; } = 3;

    /// <summary>Safety cap on research iterations to prevent infinite loops.</summary>
    public int MaxIterations { get; set; } = 10;
}

public class KnowledgeSettings
{
    /// <summary>
    /// Folder (relative to AppContext.BaseDirectory) containing the PDF
    /// knowledge base documents. Every *.pdf in this folder is loaded at startup.
    /// </summary>
    public string DocumentsFolder { get; set; } = "Documents";

    /// <summary>
    /// Short human-readable description of what the corpus contains.
    /// Surfaced to the Planner so it knows what `search_docs` covers.
    /// </summary>
    public string Description { get; set; } =
        "Contoso call-center support guidelines and the Contoso employee handbook";
}

public class DocumentSource
{
    /// <summary>Human-readable name used in citations (e.g. "Tech Support Guidelines").</summary>
    public string Name { get; set; } = "";

    /// <summary>Local file path of the document.</summary>
    public string FilePath { get; set; } = "";

    /// <summary>Optional short description passed to the Planner.</summary>
    public string Description { get; set; } = "";
}
