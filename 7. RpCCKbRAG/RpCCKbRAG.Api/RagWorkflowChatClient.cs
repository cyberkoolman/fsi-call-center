using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using RpCCKbRAG.Configuration;
using RpCCKbRAG.Models;
using RpCCKbRAG.Services;
using RpCCKbRAG.Workflow;
using System.Runtime.CompilerServices;

namespace RpCCKbRAG.Api;

/// <summary>
/// IChatClient that bridges DevUI to the ContosoKB MAF workflow.
/// Each chat turn submits the latest user message into the workflow and
/// streams the synthesised answer back to DevUI.
/// </summary>
public sealed class RagWorkflowChatClient : IChatClient
{
    private readonly Microsoft.Agents.AI.Workflows.Workflow _workflow;

    public RagWorkflowChatClient(AppSettings settings, AzureAIService ai, VectorStore store)
    {
        _workflow = ContosoKbWorkflow.Build(ai, store, settings.Pipeline);
    }

    public ChatClientMetadata Metadata => new(
        ContosoKbWorkflow.Name,
        providerUri: null,
        defaultModelId: "contoso-kb");

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var chunks = new List<string>();
        await foreach (var update in GetStreamingResponseAsync(chatMessages, options, cancellationToken))
            if (update.Text is { } t) chunks.Add(t);

        return new ChatResponse(new ChatMessage(ChatRole.Assistant, string.Concat(chunks)));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var userText = chatMessages
            .LastOrDefault(m => m.Role == ChatRole.User)?.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(userText))
        {
            yield return Text("Please ask a question about the Contoso knowledge base.");
            yield break;
        }

        Console.WriteLine($"\n[ContosoKB] Query: {userText}");

        string? answer = null;
        await using var run = await InProcessExecution.RunStreamingAsync(_workflow, new UserQuery(userText));

        await foreach (var evt in run.WatchStreamAsync())
        {
            if (evt is WorkflowOutputEvent outputEvent)
            {
                answer = outputEvent.Data as string;
                break;
            }
        }

        answer ??= "The knowledge base did not produce an answer.";

        var sentences = answer.Split([". ", ".\n"], StringSplitOptions.None);
        for (int i = 0; i < sentences.Length; i++)
        {
            var chunk = i < sentences.Length - 1 ? sentences[i] + ". " : sentences[i];
            if (string.IsNullOrWhiteSpace(chunk)) continue;
            yield return new ChatResponseUpdate
            {
                Role = ChatRole.Assistant,
                Contents = [new TextContent(chunk)]
            };
            await Task.Delay(25, ct);
        }
    }

    public object? GetService(Type serviceType, object? key = null) => null;

    public void Dispose() { }

    private static ChatResponseUpdate Text(string text) => new()
    {
        Role = ChatRole.Assistant,
        Contents = [new TextContent(text)]
    };
}
