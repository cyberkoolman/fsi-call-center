using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using RpCCAnalyticsConsole.Configuration;
using RpCCAnalyticsConsole.Services;
using RpCCAnalyticsConsole.Tools;

Console.OutputEncoding = System.Text.Encoding.UTF8;

if (args.Length == 0)
{
    Console.WriteLine("Usage: dotnet run -- \"<your question>\"");
    return;
}
var question = string.Join(' ', args);

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false)
    .Build();

var settings = config.Get<AppSettings>()
    ?? throw new InvalidOperationException("Failed to load appsettings.json.");

if (string.IsNullOrWhiteSpace(settings.AzureAI.Endpoint))
    throw new InvalidOperationException("AzureAI:Endpoint is not configured.");

var aiService = new AzureAIService(settings);
var queryTool = new QueryDbTool(settings);

var instructionsPath = Path.Combine(AppContext.BaseDirectory, "Prompts", "AnalyticsAgent.md");
var instructions = await File.ReadAllTextAsync(instructionsPath);

AITool[] tools =
[
    AIFunctionFactory.Create(queryTool.ExecuteSqlAsync)
];

AIAgent agent = aiService.CreateAgent(
    instructions: instructions,
    name: "CallCenterAnalyticsAgent",
    tools: tools);

Console.WriteLine(new string('═', 80));
Console.WriteLine("  RpCCAnalyticsConsole  |  NL → SQL → Answer  |  Microsoft Agent Framework");
Console.WriteLine(new string('═', 80));
Console.WriteLine($"  Endpoint   : {settings.AzureAI.Endpoint}");
Console.WriteLine($"  Chat model : {settings.AzureAI.ChatModel}");
Console.WriteLine($"  Database   : {SafeDbName(settings.ConnectionStrings.CallCenter)}");
Console.WriteLine($"  Question   : {question}");
Console.WriteLine(new string('─', 80));

var response = await agent.RunAsync(question);

Console.WriteLine();
Console.WriteLine("───[ Answer ]───────────────────────────────────────────────");
Console.WriteLine(response.Text);

static string SafeDbName(string cs)
{
    try
    {
        var b = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(cs);
        return $"{b.DataSource}/{b.InitialCatalog}";
    }
    catch { return "(unparseable)"; }
}
