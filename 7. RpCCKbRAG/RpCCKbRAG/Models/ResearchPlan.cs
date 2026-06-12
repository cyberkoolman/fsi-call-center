using System.Text.Json.Serialization;

namespace RpCCKbRAG.Models;

/// <summary>
/// A structured multi-step research plan created by the Planner agent.
/// </summary>
public class ResearchPlan
{
    [JsonPropertyName("steps")]
    public List<ResearchStep> Steps { get; set; } = new();
}

/// <summary>
/// A single step in the research plan. Vector-only build: tool is always "search_docs".
/// </summary>
public class ResearchStep
{
    [JsonPropertyName("subQuestion")]
    public string SubQuestion { get; set; } = "";

    [JsonPropertyName("reasoning")]
    public string Reasoning { get; set; } = "";

    /// <summary>Always "search_docs" in this build (vector-only knowledge base).</summary>
    [JsonPropertyName("tool")]
    public string Tool { get; set; } = "search_docs";
}
