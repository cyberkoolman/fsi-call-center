using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using RpCCTranscriptAnalyze.Tools.Configuration;
using RpCCTranscriptAnalyze.Tools.Services;
using RpCCTranscriptAnalyze.Tools.Tools;

Console.OutputEncoding = System.Text.Encoding.UTF8;

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

// ── Tools exposed to the agent (Microsoft Agent Framework auto-invokes these) ──
var timeInformation = new TimeInformation();
AITool[] tools =
[
    AIFunctionFactory.Create(timeInformation.GetCurrentUtcDate)
];

const string instructions = """
    You analyze customer call transcripts between Contoso Services employees and customers.

    Extract the following fields and return ONLY a single JSON object with these exact keys:

    - classified_reason           : one of "broken_item", "damaged_package", "lost_package", "late_package", "address_change", "new_package_request"
    - resolve_status              : "resolved" or "unresolved"
    - call_summary                : max 100 characters
    - customer_name
    - employee_name
    - order_number
    - customer_contact_nr         : "N/A" if not mentioned
    - new_address                 : "N/A" unless the call reason is an address change
    - sentiment_initial           : one or more of "calm", "complaining", "angry", "frustrated", "unhappy", "neutral", "happy"
    - sentiment_final             : one or more of the same set
    - satisfaction_score_initial  : integer 0 (very unsatisfied) - 10 (very satisfied)
    - satisfaction_score_final    : integer 0 - 10
    - eta                         : estimated time of arrival, or "N/A"
    - action_item                 : one or more of "no_action", "track_package", "inquire_package_status", "make_address_change", "cancel_order", "contact_customer"
    - call_date                   : today's date in YYYY-MM-DD — call the GetCurrentUtcDate tool to obtain it.

    If the customer is satisfied at the end, no follow-up is needed; otherwise note the follow-up in the action_item field.

    Respond with valid JSON only — no markdown fences, no commentary.
    """;

AIAgent agent = aiService.CreateAgent(
    instructions: instructions,
    name: "TranscriptAnalyzer",
    tools: tools);

string filePath = Path.Combine(AppContext.BaseDirectory, "input.txt");
var transcript = await File.ReadAllTextAsync(filePath);

Console.WriteLine(new string('═', 80));
Console.WriteLine("  RpCCTranscriptAnalyze.Tools  |  Microsoft Agent Framework  |  Azure AI Foundry (gpt-4o)");
Console.WriteLine(new string('═', 80));
Console.WriteLine($"  Endpoint    : {settings.AzureAI.Endpoint}");
Console.WriteLine($"  Chat model  : {settings.AzureAI.ChatModel}");
Console.WriteLine($"  Tools       : {string.Join(", ", tools.Select(t => t.Name))}");
Console.WriteLine($"  Transcript  : {filePath}  ({transcript.Length} chars)");
Console.WriteLine(new string('─', 80));

var response = await agent.RunAsync(transcript);

Console.WriteLine(response.Text);
