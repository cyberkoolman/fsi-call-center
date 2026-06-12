# 5. RpCCAnalyticsConsole

A natural-language → SQL → answer **agent** for the `Call_Center` database.
Ask a question in English; the agent writes the SQL, runs it against
`CustomerIssues`, reads the rows, and writes a two-paragraph English answer.

> **Microsoft Agent Framework (MAF) is the orchestrator here.** What used to
> be three Semantic Kernel plugins (`ConvertNLPToSQL`, `QueryDb`,
> `WriteResponse`) glued by `FunctionChoiceBehavior.Auto()` is now a **single
> agent with a single tool**. The model reasons, calls `ExecuteSqlAsync`,
> sees the JSON it got back, and continues writing the answer — all in one
> `agent.RunAsync(question)` call.

## Stack

| Concern              | Library                                    |
| -------------------- | ------------------------------------------ |
| Target framework     | `.NET 9`                                   |
| Agent runtime        | `Microsoft.Agents.AI` 1.10.0               |
| Azure OpenAI bridge  | `Microsoft.Agents.AI.OpenAI` 1.10.0        |
| Model client         | `Azure.AI.OpenAI` 2.1.0                    |
| SQL                  | `Microsoft.Data.SqlClient` 5.2.2           |
| Auth                 | `AzureKeyCredential` (with `DefaultAzureCredential` fallback) |
| Model                | `gpt-4o` on Azure AI Foundry               |

`Microsoft.SemanticKernel` is **gone**.

## Run

Pre-requisite: project #4 has populated `Call_Center.CustomerIssues` with at
least a few rows. Then:

```powershell
cd "5. RpCCAnalyticsConsole"
dotnet run -- "How many support calls were made in the last 3 months?"
```

Or use the bundled samples:

```
run01.cmd  — count calls in the last 3 months
run02.cmd  — count calls that ended with a better mood than they started
```

Sample output:

```
───[ tool: ExecuteSqlAsync ]───────────────────────────────
SELECT COUNT(*) AS CallCount
FROM CustomerIssues
WHERE CallDate >= DATEADD(MONTH, -3, CAST(GETDATE() AS DATE));
───────────────────────────────────────────────────────────

───[ Answer ]──────────────────────────────────────────────
There were 305 support calls made in the last three months.

I counted rows in the CustomerIssues table where CallDate is on or after
three months before today's date.
```

The "tool" block prints whatever SQL the agent decided to run; the "Answer"
block is the final agent response.

## How the agent is wired

```
appsettings(.Local).json ─┐
                          ▼
                    AppSettings
                          │
       ┌──────────────────┴──────────────────┐
       ▼                                     ▼
AzureAIService                       QueryDbTool(connStr)
       │                                     │
       │  CreateAgent(instructions, name,    │ [Description] ExecuteSqlAsync(string sql)
       │              tools: [ ExecuteSqlAsync ])
       ▼                                     ▼
                ChatClientAgent (MAF)
                       │
                       ▼
            await agent.RunAsync(question)
                       │
   model emits SQL ──► ExecuteSqlAsync ──► JSON rows back to model ──► final answer text
```

Internally MAF's `ChatClientAgent.RunAsync` keeps looping back to the model
until it stops emitting tool calls. That's the entirety of the
"plan → query → respond" orchestration that used to live in user-written
plugin glue.

### The native function (tool)

`Tools/QueryDbTool.ExecuteSqlAsync` is a plain `async Task<string>` method
exposed to the agent via `AIFunctionFactory.Create`. The model sees the
`[Description]` attributes on the method and the `sql` parameter; MAF
generates the JSON schema and handles the round-trip when the model emits
a tool call.

The tool **enforces SELECT-only** in code (defense in depth — the prompt
also says it, but we don't trust the LLM with the database):

```csharp
private static bool IsSelectOnly(string sql) { … }   // rejects INSERT/UPDATE/DELETE/DDL/EXEC
```

It returns the rows as a single JSON string so the model can read column
names + values directly.

### The system prompt (`Prompts/AnalyticsAgent.md`)

Carries everything the SK prompt-plugins used to carry:

- The schema of `CustomerIssues` with column-level guidance
- Hard rules (T-SQL only, SELECT only, no other tables)
- Hints on the comma-joined list columns and date arithmetic
- The required two-paragraph response format
- A worked example

This file is loaded at runtime, not embedded — edit it without rebuilding.

## Project layout

```
5. RpCCAnalyticsConsole/
├── Configuration/
│   └── AppSettings.cs            AzureAI + ConnectionStrings:CallCenter
├── Services/
│   └── AzureAIService.cs         AzureOpenAIClient + CreateAgent(...)
├── Tools/
│   └── QueryDbTool.cs            [Description] ExecuteSqlAsync — SELECT-only
├── Prompts/
│   └── AnalyticsAgent.md         system instructions (schema + rules + format)
├── Program.cs                    config → service → tool → agent → run
├── appsettings.json              localhost SQL + AzureAI template (committed)
├── appsettings.Local.json        real key + override (.gitignored)
├── run01.cmd / run02.cmd         sample questions
└── RpCCAnalyticsConsole.csproj
```

## Configuration

`appsettings.json` defaults match projects #3 and #4:

```json
{
  "AzureAI": {
    "Endpoint": "https://YOUR_RESOURCE.services.ai.azure.com/",
    "ApiKey": "",
    "ChatModel": "gpt-4o"
  },
  "ConnectionStrings": {
    "CallCenter": "Server=localhost;Database=Call_Center;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True;"
  }
}
```

`appsettings.Local.json` (git-ignored) overrides with your real key and any
remote DB connection.

## Migration notes (SK → MAF)

| Before (Semantic Kernel)                                                    | After (MAF)                                                  |
| --------------------------------------------------------------------------- | ------------------------------------------------------------ |
| `Kernel.CreateBuilder().AddAzureOpenAIChatCompletion(...)`                  | `chatClient.AsAIAgent(instructions, name, tools)`            |
| Two prompt-plugin folders (`ConvertNLPToSQL`, `WriteResponse`) with `skprompt.txt` + `config.json` | One `Prompts/AnalyticsAgent.md` system message |
| `[KernelFunction] QueryDb` registered via `ImportPluginFromType<>()`        | Plain method exposed via `AIFunctionFactory.Create(...)`     |
| `FunctionChoiceBehavior.Auto()` + `IChatCompletionService.GetChatMessageContentAsync` | `agent.RunAsync(question)` — the agent loops automatically |
| Connection string read inside the plugin via `ConfigurationBuilder`         | Strongly-typed `AppSettings` injected once at startup        |
