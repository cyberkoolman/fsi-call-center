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
if (string.IsNullOrWhiteSpace(settings.ConnectionStrings.CallCenter))
    throw new InvalidOperationException("ConnectionStrings:CallCenter is not configured.");

builder.Services.AddSingleton(settings);
builder.Services.AddSingleton<QueryDbTool>();

// Register the IChatClient that all DevUI-hosted agents share.
builder.Services.AddChatClient(AzureAIChatClientFactory.Create(settings));

// ─── Register the analytics agent for DevUI ───────────────────────────────────
//
// MAF's hosting layer instantiates a ChatClientAgent from the registered
// IChatClient + instructions, and auto-attaches every AITool added via
// .WithAITool / .WithAITools (they're stored as keyed DI services keyed by
// the agent name). DevUI lists every registered agent as a separate chat
// in the browser; AgentThread / conversation state is owned by DevUI on a
// per-conversation basis — no custom store needed.
//
var instructionsPath = Path.Combine(AppContext.BaseDirectory, "Prompts", "AnalyticsAgent.md");
var instructions = await File.ReadAllTextAsync(instructionsPath);

builder.AddAIAgent(
        name:         "CallCenterAnalytics",
        instructions: instructions)
    .WithAITool(sp => AIFunctionFactory.Create(
        sp.GetRequiredService<QueryDbTool>().ExecuteSqlAsync));

// ─── DevUI services ───────────────────────────────────────────────────────────

builder.Services.AddOpenAIResponses();
builder.Services.AddOpenAIConversations();
builder.Services.AddDevUI();

var app = builder.Build();

app.MapOpenAIResponses();
app.MapOpenAIConversations();
app.MapDevUI();

Console.WriteLine(new string('═', 60));
Console.WriteLine("  RpCCAnalyticsChat  —  DevUI");
Console.WriteLine("  http://localhost:8888/devui");
Console.WriteLine(new string('═', 60));

await app.RunAsync();


