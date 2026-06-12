using Microsoft.Agents.AI.DevUI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Hosting.OpenAI;
using Microsoft.Extensions.AI;
using RpContactCenterApi.Configuration;
using RpContactCenterApi.Services;
using RpContactCenterApi.Tools;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json",       optional: false, reloadOnChange: false)
    .AddJsonFile("appsettings.Local.json", optional: true,  reloadOnChange: false);

var settings = builder.Configuration.Get<AppSettings>()
    ?? throw new InvalidOperationException("Failed to load appsettings.json.");

if (string.IsNullOrWhiteSpace(settings.AzureAI.Endpoint))
    throw new InvalidOperationException("AzureAI:Endpoint is not configured.");
if (string.IsNullOrWhiteSpace(settings.ConnectionStrings.CallCenter))
    throw new InvalidOperationException("ConnectionStrings:CallCenter is not configured.");

// ─── Build shared services ────────────────────────────────────────────────────

var ai     = new AzureAIService(settings);
var store  = new VectorStore();
var loader = new DocumentLoader(settings);

// ─── Pre-load every PDF in Documents/ ─────────────────────────────────────────

var docsFolder = Path.Combine(AppContext.BaseDirectory, settings.Knowledge.DocumentsFolder);
if (!Directory.Exists(docsFolder))
    throw new DirectoryNotFoundException($"Knowledge documents folder not found: {docsFolder}");

var pdfFiles = Directory.GetFiles(docsFolder, "*.pdf", SearchOption.AllDirectories);
if (pdfFiles.Length == 0)
    throw new InvalidOperationException($"No PDF files found under {docsFolder}.");

var sources = pdfFiles
    .Select(p => new DocumentSource
    {
        Name     = Path.GetFileNameWithoutExtension(p).Replace('_', ' '),
        FilePath = p,
    })
    .ToList();

Console.WriteLine(new string('═', 60));
Console.WriteLine($"  Indexing {sources.Count} PDF document(s) from {docsFolder}");
Console.WriteLine(new string('═', 60));

var allDocs = await loader.LoadAllAsync(sources, ai);
store.AddDocuments(allDocs);

Console.WriteLine($"  → Indexed {store.DocumentCount} chunks across {sources.Count} document(s).");

// ─── DI: shared singletons + tools ────────────────────────────────────────────

builder.Services.AddSingleton(settings);
builder.Services.AddSingleton(ai);
builder.Services.AddSingleton(store);
builder.Services.AddSingleton<QueryDbTool>();
builder.Services.AddSingleton<KnowledgeBaseTool>();

// IChatClient used by the unified MAF agent.
builder.Services.AddChatClient(AzureAIChatClientFactory.Create(settings));

// ─── Register the unified ContactCenter agent ─────────────────────────────────

var instructionsPath = Path.Combine(AppContext.BaseDirectory, "Prompts", "ContactCenterAgent.md");
var instructions     = await File.ReadAllTextAsync(instructionsPath);

builder.AddAIAgent(
        name:         "ContactCenter",
        instructions: instructions)
    .WithAITool(sp => AIFunctionFactory.Create(
        sp.GetRequiredService<QueryDbTool>().ExecuteSqlAsync))
    .WithAITool(sp => AIFunctionFactory.Create(
        sp.GetRequiredService<KnowledgeBaseTool>().SearchKnowledgeBaseAsync));

// ─── DevUI services ───────────────────────────────────────────────────────────

builder.Services.AddOpenAIResponses();
builder.Services.AddOpenAIConversations();
builder.Services.AddDevUI();

var app = builder.Build();

app.MapOpenAIResponses();
app.MapOpenAIConversations();
app.MapDevUI();

Console.WriteLine(new string('═', 60));
Console.WriteLine("  RpContactCenterApi  —  ContactCenter DevUI");
Console.WriteLine("  http://localhost:8888/devui");
Console.WriteLine(new string('═', 60));

await app.RunAsync();
