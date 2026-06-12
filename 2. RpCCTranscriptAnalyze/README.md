# 2. RpCCTranscriptAnalyze — Transcript → Structured JSON

📝 .NET 9 console app that reads a customer-call transcript (`input.txt`) and uses **Azure AI Foundry** (`gpt-4o`) to extract a 15-field structured JSON record describing the call.

## What it does

1. Loads configuration from `appsettings.json` (template) merged with `appsettings.Local.json` (developer-only, git-ignored).
2. Builds an `AzureOpenAIClient` against an Azure AI Foundry endpoint, using an API key when one is present and falling back to `DefaultAzureCredential` (Entra ID) otherwise.
3. Reads the transcript from `input.txt`.
4. Sends a single **system + user** chat completion to the configured chat deployment (`gpt-4o`), with a strict prompt that defines exactly which JSON fields the model must produce.
5. Prints the model's JSON response to stdout.

### Extracted fields

| Field | Description |
|---|---|
| `classified_reason` | `broken_item`, `damaged_package`, `lost_package`, `late_package`, `address_change`, `new_package_request` |
| `resolve_status` | `resolved` / `unresolved` |
| `call_summary` | ≤ 100 chars |
| `customer_name`, `employee_name`, `order_number`, `customer_contact_nr`, `new_address` | extracted from the conversation |
| `sentiment_initial`, `sentiment_final` | one or more of `calm`, `complaining`, `angry`, `frustrated`, `unhappy`, `neutral`, `happy` |
| `satisfaction_score_initial`, `satisfaction_score_final` | integer 0–10 |
| `eta` | estimated arrival, or `N/A` |
| `action_item` | one or more of `no_action`, `track_package`, `inquire_package_status`, `make_address_change`, `cancel_order`, `contact_customer` |
| `call_date` | `YYYY-MM-DD` |

## A note on Microsoft Agent Framework (MAF)

This project does **not** use Microsoft Agent Framework. The task is a single, stateless prompt → JSON extraction with no tools, no memory, and no multi-turn reasoning, so the simplest fit is a direct **Azure OpenAI chat completion** through the v2 SDK (`Azure.AI.OpenAI` 2.x → `OpenAI.Chat.ChatClient.CompleteChatAsync`). Adding MAF here would only wrap the same call in an `AIAgent` for no behavioural gain.

MAF (`Microsoft.Agents.AI`, `Microsoft.Agents.AI.OpenAI`, `Microsoft.Agents.AI.Workflows`) will be introduced in later projects of this suite where it earns its keep:

- **Project #3 (`RpCCTranscriptAnalyze.Tools`)** — adds a native `TimeInformation` tool, a natural fit for a MAF agent with auto function-calling.
- **Projects #5 / #8 (Text-to-SQL + RAG)** — multi-tool agents (`QueryDbPlugin`, `RAGPlugin`) and conversation state per `conversationId`.
- **Project #7 (KB RAG)** — retrieval + answer composition workflow, ideal for `Microsoft.Agents.AI.Workflows`.

The codebase deliberately follows the same `AzureAIService` / `AppSettings` shape as the **`Agentic.RAG`** reference project so subsequent migrations can layer MAF on top of an already-consistent foundation.

## Tech stack

| Component | Version / Service |
|---|---|
| Runtime | .NET 9 |
| Azure SDK | `Azure.AI.OpenAI` 2.1.0 + `Azure.Identity` 1.13.2 |
| Configuration | `Microsoft.Extensions.Configuration[.Json/.Binder]` 9.0.3 |
| Endpoint | Azure AI Foundry (`*.services.ai.azure.com`) |
| Model | `gpt-4o` chat-completion deployment |
| Auth | API key via `AzureKeyCredential` → fallback `DefaultAzureCredential` |
| `NoWarn` | `MAAI001` (suppresses MAF preview warnings, applied project-wide for consistency with sibling projects) |

## Project layout

```
2. RpCCTranscriptAnalyze/
├── Configuration/
│   └── AppSettings.cs          # strongly-typed settings: AzureAI { Endpoint, ApiKey, ChatModel }
├── Services/
│   └── AzureAIService.cs       # AzureOpenAIClient wrapper: CompleteAsync, CompleteStructuredAsync<T>, StripMarkdownFences
├── Program.cs                  # config bootstrap → AzureAIService → CompleteAsync → stdout
├── appsettings.json            # public template (placeholder endpoint, blank key) — committed
├── appsettings.Local.json      # real endpoint + key — git-ignored
├── input.txt                   # sample call transcript
└── RpCCTranscriptAnalyze.csproj
```

## Configuration

`appsettings.json` (committed):

```json
{
    "AzureAI": {
        "Endpoint": "https://YOUR_RESOURCE.services.ai.azure.com/",
        "ApiKey": "",
        "ChatModel": "gpt-4o"
    }
}
```

`appsettings.Local.json` (NEVER commit — already covered by `.gitignore`):

```json
{
    "AzureAI": {
        "Endpoint": "https://<your-foundry>.services.ai.azure.com/",
        "ApiKey": "<key>",
        "ChatModel": "gpt-4o"
    }
}
```

Leave `ApiKey` blank to use Entra ID via `DefaultAzureCredential` (your `az login` session, Visual Studio identity, or Managed Identity in Azure). In that case the signed-in principal needs **Cognitive Services OpenAI User** role on the Foundry resource.

## Run locally

```powershell
cd "2. RpCCTranscriptAnalyze"
dotnet run
```

Expected output:

```
════════════════════════════════════════════════════════════════════════════════
  RpCCTranscriptAnalyze  |  Azure AI Foundry (gpt-4o)  |  Microsoft Agent Framework
════════════════════════════════════════════════════════════════════════════════
  Endpoint    : https://<resource>.services.ai.azure.com/
  Chat model  : gpt-4o
  Transcript  : .../input.txt  (1310 chars)
────────────────────────────────────────────────────────────────────────────────
```json
{
  "classified_reason": "lost_package",
  "resolve_status": "unresolved",
  "call_summary": "Customer inquired about a delayed package with no current status.",
  "customer_name": "James Brown",
  ...
}
```

## Migration notes (from the original .NET 8 / Semantic Kernel version)

| Before | After |
|---|---|
| `net8.0` | **`net9.0`** |
| `Microsoft.SemanticKernel` 1.32 + `Kernel.CreateBuilder().AddAzureOpenAIChatCompletion(...)` | `Azure.AI.OpenAI` 2.1.0 + `AzureOpenAIClient.GetChatClient(...).CompleteChatAsync(...)` |
| Inline `kernel.InvokePromptAsync(promptString, new() { ["input"] = input })` | Strongly-typed `AzureAIService.CompleteAsync(systemPrompt, userMessage)` |
| `appsettings.json` checked in with real keys | `appsettings.json` template (committed) + `appsettings.Local.json` (git-ignored) |
| Flat single-file program | `Configuration/`, `Services/` folders matching the `Agentic.RAG` reference layout |
