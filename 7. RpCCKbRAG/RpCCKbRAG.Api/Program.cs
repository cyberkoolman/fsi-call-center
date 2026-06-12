using Microsoft.Agents.AI.DevUI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Hosting.OpenAI;
using RpCCKbRAG.Api;
using RpCCKbRAG.Configuration;
using RpCCKbRAG.Models;
using RpCCKbRAG.Services;

Console.OutputEncoding = System.Text.Encoding.UTF8;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json",       optional: false, reloadOnChange: false)
    .AddJsonFile("appsettings.Local.json", optional: true,  reloadOnChange: false);

var settings = builder.Configuration.Get<AppSettings>()
    ?? throw new InvalidOperationException("Failed to load appsettings.");

if (string.IsNullOrWhiteSpace(settings.AzureAI.Endpoint))
    throw new InvalidOperationException("AzureAI:Endpoint is not configured.");

// ─── Build shared services ────────────────────────────────────────────────────

var ai      = new AzureAIService(settings);
var store   = new VectorStore();
var loader  = new DocumentLoader(settings);
var kbState = new KnowledgeBaseState();

// ─── Pre-load every PDF in Documents/ ─────────────────────────────────────────

var docsFolder = Path.Combine(AppContext.BaseDirectory, settings.Knowledge.DocumentsFolder);
if (!Directory.Exists(docsFolder))
    throw new DirectoryNotFoundException(
        $"Knowledge documents folder not found: {docsFolder}");

var pdfFiles = Directory.GetFiles(docsFolder, "*.pdf", SearchOption.AllDirectories);
if (pdfFiles.Length == 0)
    throw new InvalidOperationException(
        $"No PDF files found under {docsFolder}.");

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
foreach (var s in sources) kbState.AppendSource(s.Name);

// Override description with the curated one from appsettings if present.
if (!string.IsNullOrWhiteSpace(settings.Knowledge.Description))
    kbState.Description = settings.Knowledge.Description;

Console.WriteLine($"  → Indexed {store.DocumentCount} chunks across {sources.Count} document(s).");

// ─── Register both pipelines as DevUI agents ──────────────────────────────────

var agenticClient = new RagWorkflowChatClient(settings, ai, store, kbState, useOneShot: false);
var oneShotClient = new RagWorkflowChatClient(settings, ai, store, kbState, useOneShot: true);

builder.AddAIAgent(
    name:         "KbRAG-DeepThink",
    instructions: "You are a deep-thinking Contoso call-center knowledge-base assistant. " +
                  "You decompose complex questions into a multi-step research plan, retrieve " +
                  "and reflect iteratively over the indexed support guidelines and employee " +
                  "handbook, and synthesise a comprehensive cited answer.",
    chatClient:   agenticClient);

builder.AddAIAgent(
    name:         "KbRAG-OneShot",
    instructions: "You are a simple Contoso call-center knowledge-base assistant. " +
                  "You retrieve relevant passages from the indexed PDFs and answer " +
                  "in a single pass, with no planning or reflection.",
    chatClient:   oneShotClient);

// ─── DevUI services ───────────────────────────────────────────────────────────

builder.Services.AddOpenAIResponses();
builder.Services.AddOpenAIConversations();
builder.Services.AddDevUI();

WebApplication app = builder.Build();

app.MapOpenAIResponses();
app.MapOpenAIConversations();
app.MapDevUI();

Console.WriteLine(new string('═', 60));
Console.WriteLine("  RpCCKbRAG  —  DevUI");
Console.WriteLine("  http://localhost:8888/devui");
Console.WriteLine(new string('═', 60));

await app.RunAsync();
