# 6. RpCCAnalyticsChat — Conversational Call‑Center Analytics (MAF + DevUI)

A natural‑language analytics chat over the `Call_Center` SQL Server database.
Users ask questions in plain English ("How many escalations last month?",
"Top 5 agents by negative sentiment?") and an LLM agent translates each
question into safe `SELECT` SQL, executes it, and answers grounded in the
returned rows — while **preserving multi‑turn context** ("now break that
down by week").

## What this replaces

The original solution shipped two components:

| Original | Replaced by |
|----------|-------------|
| `RpContactCenterApi` — ASP.NET Core controller + Semantic Kernel + a hand‑rolled `ConcurrentDictionary<string, ChatHistory>` to track conversations | A **Microsoft Agent Framework (MAF) hosted agent** with conversation state owned by DevUI |
| `RpContactCenterUI` — Streamlit page that POSTed to the API | The **DevUI** SPA shipped inside `Microsoft.Agents.AI.DevUI` (no custom UI to maintain) |

Both projects were deleted; the entire experience is now one .NET 9 web
host that boots an agent and serves the DevUI from the same process.

## Architecture

```
Browser  ──►  http://localhost:8888/devui   (DevUI SPA — embedded in MAF package)
                  │
                  ├─►  /v1/responses         (OpenAI Responses API surface, served by MAF)
                  └─►  /v1/conversations     (Conversation state owned by MAF)
                              │
                              ▼
                      ChatClientAgent  ◄── instructions: Prompts/AnalyticsAgent.md
                              │
                              ├── IChatClient  ─►  Azure OpenAI (gpt‑4o)
                              │
                              └── AITool: ExecuteSqlAsync  ─►  Call_Center (SQL Server)
```

### Why DevUI instead of a custom controller + Streamlit?

* **No conversation plumbing.** DevUI persists threads via the OpenAI
  Conversations API (`/v1/conversations`) — every turn within a thread
  is automatically rehydrated for the LLM. The old `ConcurrentDictionary`
  is gone.
* **No bespoke UI.** DevUI ships a complete chat experience (multi‑agent
  side panel, streaming, tool‑call inspector, thread switcher). Streamlit
  is no longer required.
* **Multi‑agent ready.** Adding another agent is a single
  `builder.AddAIAgent("Name", instructions).WithAITool(...)` line; it
  shows up immediately in the DevUI sidebar.

## Microsoft Agent Framework usage

Everything that matters lives in `Program.cs`:

```csharp
// 1. The shared chat client — Azure OpenAI behind the M.E.AI abstraction
builder.Services.AddChatClient(AzureAIChatClientFactory.Create(settings));

// 2. The agent. WithAITool registers the tool as a keyed DI service
//    (key = agent name); MAF's hosting layer auto-attaches every tool
//    keyed to the agent when it constructs the ChatClientAgent.
builder.AddAIAgent(
        name:         "CallCenterAnalytics",
        instructions: instructions)
    .WithAITool(sp => AIFunctionFactory.Create(
        sp.GetRequiredService<QueryDbTool>().ExecuteSqlAsync));

// 3. DevUI + the OpenAI surface it speaks
builder.Services.AddOpenAIResponses();
builder.Services.AddOpenAIConversations();
builder.Services.AddDevUI();

var app = builder.Build();
app.MapOpenAIResponses();
app.MapOpenAIConversations();
app.MapDevUI();
```

That is the entire wiring. There are no controllers, no minimal‑API
endpoints, and no manual `AgentThread` bookkeeping — DevUI does it all.

### Components

| File | Role |
|------|------|
| `Program.cs` | Hosts the agent + DevUI. |
| `Services/AzureAIChatClientFactory.cs` | Builds an `IChatClient` from `AzureAI` settings (key or `DefaultAzureCredential`). |
| `Tools/QueryDbTool.cs` | Single `ExecuteSqlAsync(string tsql)` tool. SELECT‑only guard, parameter‑less, returns rows as JSON for the model to read. |
| `Prompts/AnalyticsAgent.md` | The system prompt — same text used by project #5; describes the schema and forbids destructive SQL. |
| `Configuration/AppSettings.cs` | Strongly‑typed binding for `AzureAI` + `ConnectionStrings.CallCenter`. |
| `appsettings.json` | Template (committed). |
| `appsettings.Local.json` | Real endpoint / API key / connection string (git‑ignored). |

## Configuration

`appsettings.Local.json` (next to `appsettings.json`, never committed):

```json
{
  "AzureAI": {
    "Endpoint":  "https://<your-aoai>.openai.azure.com/",
    "ApiKey":    "<key>",
    "ChatModel": "gpt-4o"
  },
  "ConnectionStrings": {
    "CallCenter": "Server=localhost;Database=Call_Center;Integrated Security=True;TrustServerCertificate=True"
  }
}
```

If `ApiKey` is omitted, `DefaultAzureCredential` is used (works with
`az login`, managed identity, etc.).

## Run it

```powershell
cd "6. RpCCAnalyticsChat\RpContactCenterApi"
dotnet run
```

Then open <http://localhost:8888/devui>. Pick **CallCenterAnalytics**
in the sidebar and ask:

* "How many calls did we handle last month?"
* "Show me the 5 agents with the most negative‑sentiment calls."
* "Now show that for just the billing team."  ← multi‑turn context

Open the **Tool Calls** panel in DevUI to see the SQL the agent
generated and the rows it received back.

## Package versions

* `Microsoft.Agents.AI.DevUI` `1.10.0-preview.260610.1`
* `Microsoft.Agents.AI.Hosting` `1.10.0-preview.260610.1`
* `Microsoft.Agents.AI.Hosting.OpenAI` `1.10.0-alpha.260610.1`
* `Azure.AI.OpenAI` `2.1.0`
* `Microsoft.Extensions.AI.OpenAI` `10.6.0`
* `Microsoft.Data.SqlClient` `5.2.2`
* Target framework: **net9.0**

> The DevUI / Hosting packages are still pre‑release at the time of
> migration; bump them to a `1.10.0` GA when one ships.
