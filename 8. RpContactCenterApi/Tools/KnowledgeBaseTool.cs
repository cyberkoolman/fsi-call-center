using System.ComponentModel;
using System.Text.Json;
using RpContactCenterApi.Configuration;
using RpContactCenterApi.Services;

namespace RpContactCenterApi.Tools;

/// <summary>
/// Native function exposed to the unified MAF agent for retrieving passages from
/// the indexed Contoso knowledge-base PDFs (Tech-Support Guidelines, Employee
/// Handbook, etc.). The tool embeds the query, runs cosine-similarity search
/// over the in-memory <see cref="VectorStore"/>, and returns the top-K chunks as
/// a JSON array. The agent synthesises the final answer from those chunks.
/// </summary>
public class KnowledgeBaseTool
{
    private readonly AzureAIService _ai;
    private readonly VectorStore    _store;
    private readonly int            _topK;

    public KnowledgeBaseTool(AzureAIService ai, VectorStore store, AppSettings settings)
    {
        _ai    = ai;
        _store = store;
        _topK  = settings.Pipeline.TopK;
    }

    [Description(
        "Searches the Contoso knowledge-base PDFs (Tech-Support Guidelines and " +
        "Employee Handbook) for passages relevant to the query and returns the " +
        "top matches as JSON. Use this for policy, procedure, escalation, " +
        "handbook, benefits, or guideline questions — anything that is not a " +
        "question about caller-data analytics in the SQL database.")]
    public async Task<string> SearchKnowledgeBaseAsync(
        [Description("A natural-language search query about Contoso policies or procedures.")]
        string query)
    {
        Console.WriteLine();
        Console.WriteLine("───[ tool: SearchKnowledgeBaseAsync ]───────────────────────");
        Console.WriteLine($"  Query: {query}");

        var embedding = await _ai.GenerateEmbeddingAsync(query);
        var docs      = _store.VectorSearch(embedding, _topK);

        Console.WriteLine($"  Retrieved {docs.Count} chunk(s).");
        Console.WriteLine("────────────────────────────────────────────────────────────");

        if (docs.Count == 0)
            return "[]";

        var payload = docs.Select(d => new
        {
            source  = d.Source,
            section = d.Section,
            content = d.Content
        });

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = false });
    }
}
