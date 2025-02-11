using Microsoft.KernelMemory;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var configuration = new ConfigurationBuilder()
                        .AddJsonFile("appsettings.json")
                        .Build();
string apiKey = configuration["AzureOpenAI:ApiKey"];
string deploymentChatName = configuration["AzureOpenAI:DeploymentChatName"];
string deploymentEmbbedding = configuration["AzureOpenAI:DeploymentEmbeddingName"];
string endpoint = configuration["AzureOpenAI:Endpoint"];

// Section 1: Create RAG content source with Kernel Memory
var embeddingConfig = new AzureOpenAIConfig
{
    APIKey = apiKey,
    Deployment = deploymentEmbbedding,
    Endpoint = endpoint,
    APIType = AzureOpenAIConfig.APITypes.EmbeddingGeneration,
    Auth = AzureOpenAIConfig.AuthTypes.APIKey
};
var chatConfig = new AzureOpenAIConfig
{
    APIKey = apiKey,
    Deployment = deploymentChatName,
    Endpoint = endpoint,
    APIType = AzureOpenAIConfig.APITypes.ChatCompletion,
    Auth = AzureOpenAIConfig.AuthTypes.APIKey
};

var kernelMemory = new KernelMemoryBuilder()
    .WithAzureOpenAITextGeneration(chatConfig)
    .WithAzureOpenAITextEmbeddingGeneration(embeddingConfig)
    .WithSimpleVectorDb()
    .Build<MemoryServerless>();

// Import documents
var documentsDirectory = Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Documents");
var documentFiles = Directory.GetFiles(documentsDirectory);

foreach (var documentFile in documentFiles)
{
    await kernelMemory.ImportDocumentAsync(documentFile);
}

// Section 2: Instanticate a Kernel with BAsic Azure OpenAI & Logging
var kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.Services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Trace));
kernelBuilder.AddAzureOpenAIChatCompletion(deploymentChatName, endpoint, apiKey);
var kernel = kernelBuilder.Build();

// Section 3: Integrate a Kernel with the RAG Memory
var plugin = new MemoryPlugin(kernelMemory, waitForIngestionToComplete: true);
kernel.ImportPluginFromObject(plugin, "memory");

// Section 4: Enable the function calling
PromptExecutionSettings settings = new() { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() };

// Search KernelMemory
var input = "Who should I contact for customers shipping problems?";
var searchResult = await kernelMemory.AskAsync(input);
// var searchResult = await kernelMemory.SearchAsync(input);

Console.WriteLine($"{searchResult.Result}\n");

// Citations
foreach(var source in searchResult.RelevantSources)
{
	var citationUpdates = $"{source.Partitions.First().LastUpdate:D}";
	var citation = $"• {source.SourceName} -- {citationUpdates}";

	Console.WriteLine($"{citation}");
}