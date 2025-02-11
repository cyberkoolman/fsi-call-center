using Microsoft.KernelMemory;
using Microsoft.OpenApi.Models;
using Microsoft.SemanticKernel;

var builder = WebApplication.CreateBuilder(args);

// Load configuration from appsettings.json
builder.Configuration.AddJsonFile("appsettings.json");

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "FSI Contact Center PoC API", Version = "v1" });
});

builder.Services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Trace));

// Build the kernel
var configuration = builder.Configuration;
string apiKey = configuration["AzureOpenAI:ApiKey"];
string deploymentChatName = configuration["AzureOpenAI:DeploymentChatName"];
string deploymentEmbbedding = configuration["AzureOpenAI:DeploymentEmbeddingName"];
string endpoint = configuration["AzureOpenAI:Endpoint"];

// Setting up Kernel Memory
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

// Register RAGPlugin with kernelMemory
builder.Services.AddSingleton<IKernelMemory>(kernelMemory);

var kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.Services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Trace));
kernelBuilder.AddAzureOpenAIChatCompletion(deploymentChatName, endpoint, apiKey);
var kernel = kernelBuilder.Build();

// Import Plugin for accessing Database
var nsqPluginDirectoryPath = Path.Combine(Directory.GetCurrentDirectory(), "Plugins", "NlpToSqlPlugin");
kernel.ImportPluginFromPromptDirectory(nsqPluginDirectoryPath);
kernel.ImportPluginFromType<QueryDbPlugin>();

// Integrate a Kernel with the RAG Memory
// var ragPlugin = new MemoryPlugin(kernelMemory, waitForIngestionToComplete: true);
var ragPlugin = new RAGPlugin(kernelMemory);
kernel.ImportPluginFromObject(ragPlugin);

// Enable function calling
PromptExecutionSettings settings = new() { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() };

builder.Services.AddSingleton(kernel);
builder.Services.AddSingleton(settings);

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "FSI Contact Center PoC API v1"));    
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();