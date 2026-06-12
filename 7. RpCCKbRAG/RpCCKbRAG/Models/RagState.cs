namespace RpCCKbRAG.Models;

/// <summary>
/// Central shared state passed through the entire workflow via IWorkflowContext.
/// Every executor reads from and writes to this object to track research progress.
/// </summary>
public class RagState
{
    public string UserQuery { get; set; } = "";
    public List<ResearchStep> Plan { get; set; } = new();
    public int CurrentStepIndex { get; set; } = 0;
    public List<string> ResearchHistory { get; set; } = new();
    public List<string> DistilledContexts { get; set; } = new();
    public int IterationCount { get; set; } = 0;
    public const int MaxIterations = 10;
}
