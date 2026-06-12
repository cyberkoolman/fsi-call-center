namespace RpContactCenterApi.Models;

/// <summary>
/// Represents a single document chunk in the knowledge base.
/// </summary>
public record RagDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Content { get; set; } = "";
    public string Source { get; set; } = "";
    public string Section { get; set; } = "";
    public float[] Embedding { get; set; } = Array.Empty<float>();
    public float RelevanceScore { get; set; } = 0f;
    public string Url { get; set; } = "";
}
