namespace RpCCKbRAG.Configuration;

public class AppSettings
{
    public AzureAISettings   AzureAI   { get; set; } = new();
    public PipelineSettings  Pipeline  { get; set; } = new();
    public KnowledgeSettings Knowledge { get; set; } = new();
}

public class AzureAISettings
{
    /// <summary>Azure OpenAI endpoint URL.</summary>
    public string Endpoint { get; set; } = "";

    /// <summary>API key. Leave empty to use DefaultAzureCredential.</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>Chat deployment name used to synthesise the final answer.</summary>
    public string ChatModel { get; set; } = "gpt-4o";

    /// <summary>Embedding deployment name used to vectorise chunks and queries.</summary>
    public string EmbeddingModel { get; set; } = "text-embedding-3-large";
}

public class PipelineSettings
{
    /// <summary>Approximate token size for each document chunk.</summary>
    public int ChunkSize { get; set; } = 500;

    /// <summary>Token overlap between consecutive chunks.</summary>
    public int ChunkOverlap { get; set; } = 50;

    /// <summary>Number of chunks to retrieve per query.</summary>
    public int TopK { get; set; } = 5;
}

public class KnowledgeSettings
{
    /// <summary>
    /// Folder (relative to AppContext.BaseDirectory) containing the PDF
    /// knowledge base. Every *.pdf in the folder is loaded at startup.
    /// </summary>
    public string DocumentsFolder { get; set; } = "Documents";
}

public class DocumentSource
{
    /// <summary>Human-readable name used in citations.</summary>
    public string Name { get; set; } = "";

    /// <summary>Local file path of the document.</summary>
    public string FilePath { get; set; } = "";
}
