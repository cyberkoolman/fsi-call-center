namespace RpCCKbRAG.Models;

/// <summary>
/// Shared description of the loaded knowledge base. Surfaced to the Planner so
/// it can describe what `search_docs` covers when decomposing a query.
/// </summary>
public class KnowledgeBaseState
{
    private readonly object _lock = new();
    private bool   _hasSource;
    private string _description = "";

    public bool HasSource
    {
        get { lock (_lock) return _hasSource; }
        set { lock (_lock) _hasSource = value; }
    }

    public string Description
    {
        get { lock (_lock) return _description; }
        set { lock (_lock) _description = value; }
    }

    public void AppendSource(string name)
    {
        lock (_lock)
        {
            _hasSource   = true;
            _description = string.IsNullOrEmpty(_description)
                ? name
                : _description + ", " + name;
        }
    }
}
