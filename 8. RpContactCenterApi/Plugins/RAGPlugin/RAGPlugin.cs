using Microsoft.SemanticKernel;
using System.ComponentModel;
using Microsoft.KernelMemory;

public class RAGPlugin
{
    private readonly IKernelMemory _kernelMemory;

    public RAGPlugin(IKernelMemory kernelMemory)
    {
        _kernelMemory = kernelMemory;
    }

    [KernelFunction("QueryRAG")]
    [Description("Query to RAG for asking about guidelines, when user asks with @guidelines")]
    public async Task<string> QueryRAG([Description("Search query to be executed")] string input)
    {
        var searchResult = await _kernelMemory.AskAsync(input);
        // var searchResult = await kernelMemory.SearchAsync(input);

        Console.WriteLine($"{searchResult.Result}\n");
        var response = searchResult.Result;

        // Citations
        foreach(var source in searchResult.RelevantSources)
        {
            var citationUpdates = $"{source.Partitions.First().LastUpdate:D}";
            var citation = $"• {source.SourceName} -- {citationUpdates}";

            response += $"{citation}";
        }
        return response;
    }
}