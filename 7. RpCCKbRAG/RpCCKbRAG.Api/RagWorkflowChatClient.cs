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
        var userText = chatMessages
            .LastOrDefault(m => m.Role == ChatRole.User)?.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(userText))
            return new ChatResponse([new ChatMessage(ChatRole.Assistant, "Please ask a question about the Contoso knowledge base.")]);

        Console.WriteLine($"\n[ContosoKB] Query: {userText}");

        AnswerResult? answerResult = null;
        await using var run = await InProcessExecution.RunStreamingAsync(_workflow, new UserQuery(userText));

        await foreach (var evt in run.WatchStreamAsync())
        {
            if (evt is WorkflowOutputEvent outputEvent && outputEvent.Data is AnswerResult ar)
            {
                answerResult = ar;
                break;
            }
        }

        var answer = answerResult?.Text ?? "The knowledge base did not produce an answer.";
        var response = new ChatResponse([new ChatMessage(ChatRole.Assistant, answer)]);

        if (answerResult is not null && answerResult.TotalTokens > 0)
        {
            response.Usage = new UsageDetails
            {
                InputTokenCount = answerResult.InputTokens,
                OutputTokenCount = answerResult.OutputTokens,
                TotalTokenCount = answerResult.TotalTokens
            };
        }

        return response;
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var response = await GetResponseAsync(chatMessages, options, ct);
        var answer = response.Text ?? "";

        var sentences = answer.Split([". ", ".\n"], StringSplitOptions.None);
        for (int i = 0; i < sentences.Length; i++)
        {
            var chunk = i < sentences.Length - 1 ? sentences[i] + ". " : sentences[i];
            if (string.IsNullOrWhiteSpace(chunk)) continue;

            var update = new ChatResponseUpdate
            {
                Role = ChatRole.Assistant,
                Contents = [new TextContent(chunk)]
            };

            if (i == sentences.Length - 1 && response.Usage is not null)
            {
                update.Contents.Add(new UsageContent(response.Usage));
            }

            yield return update;
            await Task.Delay(25, ct);
        }
    }

    public object? GetService(Type serviceType, object? key = null) => null;

    public void Dispose() { }
}
