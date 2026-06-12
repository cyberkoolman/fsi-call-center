using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using RpContactCenterApi.Configuration;

namespace RpContactCenterApi.Services;

/// <summary>
/// Builds an <see cref="IChatClient"/> that targets the configured Azure
/// OpenAI / Azure AI Foundry chat deployment. The DevUI-hosted agent
/// resolves this from DI when handling messages.
/// </summary>
public static class AzureAIChatClientFactory
{
    public static IChatClient Create(AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.AzureAI.Endpoint))
            throw new InvalidOperationException("AzureAI:Endpoint is not configured.");

        AzureOpenAIClient client = !string.IsNullOrWhiteSpace(settings.AzureAI.ApiKey)
            ? new AzureOpenAIClient(new Uri(settings.AzureAI.Endpoint), new AzureKeyCredential(settings.AzureAI.ApiKey))
            : new AzureOpenAIClient(new Uri(settings.AzureAI.Endpoint), new DefaultAzureCredential());

        return client.GetChatClient(settings.AzureAI.ChatModel).AsIChatClient();
    }
}


