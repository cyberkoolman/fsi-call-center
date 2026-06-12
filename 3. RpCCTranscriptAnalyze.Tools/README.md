# 3. RpCCTranscriptAnalyze.Tools

Extracts the same structured analysis as project #2 from a customer-call
transcript — **but does it through a Microsoft Agent Framework (MAF) agent
that has a native `.NET` function attached as a tool**.

This is the first project in the migration where MAF earns its keep: the
extraction prompt requires `call_date` to be **today's date in UTC**, and
the LLM cannot know that on its own. Instead of hard-coding the date in the
prompt or post-processing the response, the agent **automatically calls a
local C# method** (`TimeInformation.GetCurrentUtcDate`) when it needs the
value, then incorporates the result into the JSON it emits.

## Stack

| Concern                 | Library                                    |
| ----------------------- | ------------------------------------------ |
| Target framework        | `.NET 9`                                   |
| Agent runtime           | `Microsoft.Agents.AI` 1.10.0               |
| Azure OpenAI bridge     | `Microsoft.Agents.AI.OpenAI` 1.10.0        |
| Model client            | `Azure.AI.OpenAI` 2.1.0                    |
| Tool definition surface | `Microsoft.Extensions.AI` (`AITool`, `AIFunctionFactory`) |
| Auth                    | `AzureKeyCredential` (with `DefaultAzureCredential` fallback) |
| Model                   | `gpt-4o` on Azure AI Foundry               |

`Microsoft.SemanticKernel` is **gone** — its `[KernelFunction]` /
`FunctionChoiceBehavior.Auto()` mechanic is replaced by MAF's
`AIFunctionFactory.Create(method)` + automatic tool invocation built into
`ChatClientAgent.RunAsync`.

## How it works

```
appsettings(.Local).json ─┐
                          ▼
                  AppSettings (strongly typed)
                          │
                          ▼
         AzureAIService(settings)
            │   AzureOpenAIClient(endpoint, AzureKeyCredential)
            │
            └── CreateAgent(instructions, name, tools)
                    │   chatClient.AsAIAgent(instructions, name, tools)
                    ▼
                 AIAgent  ◄── tools: [ AIFunctionFactory.Create(timeInfo.GetCurrentUtcDate) ]
                    │
                    ▼
        await agent.RunAsync(transcript)
                    │
                    ├── model emits tool call → MAF invokes GetCurrentUtcDate()
                    ├── MAF feeds the result back to the model
                    └── model returns final JSON with call_date filled in
```

### The tool

`Tools/TimeInformation.cs` — plain C# class, no SK attributes:

```csharp
public class TimeInformation
{
    [Description("Returns today's date in UTC (yyyy-MM-dd).")]
    public string GetCurrentUtcDate() => DateTime.UtcNow.ToString("yyyy-MM-dd");
}
```

`AIFunctionFactory.Create(timeInformation.GetCurrentUtcDate)` produces an
`AITool` whose JSON schema is generated from the method signature and the
`[Description]` attribute. The agent receives this tool, decides on its own
when to call it, and `RunAsync` handles the round-trip transparently.

### The agent

```csharp
AIAgent agent = aiService.CreateAgent(
    instructions: extractionPrompt,
    name: "TranscriptAnalyzer",
    tools: [ AIFunctionFactory.Create(timeInformation.GetCurrentUtcDate) ]);

var response = await agent.RunAsync(transcript);
Console.WriteLine(response.Text);
```

That's the whole orchestration. No manual function-call loop, no kernel,
no plugin registration ceremony.

## Project layout

```
3. RpCCTranscriptAnalyze.Tools/
├── Configuration/
│   └── AppSettings.cs           strongly-typed config bound from appsettings*.json
├── Services/
│   └── AzureAIService.cs        wraps AzureOpenAIClient + CreateAgent(...)
├── Tools/
│   └── TimeInformation.cs       native function exposed to the agent
├── Program.cs                   bootstraps config, builds the agent, runs it
├── appsettings.json             template (no secrets, committed)
├── appsettings.Local.json       real endpoint + key (.gitignored)
├── input.txt                    sample transcript
└── RpCCTranscriptAnalyze.csproj
```

## Configuration

`appsettings.json` (committed, template):

```json
{
  "AzureAI": {
    "Endpoint": "https://YOUR_RESOURCE.services.ai.azure.com/",
    "ApiKey": "",
    "ChatModel": "gpt-4o"
  }
}
```

`appsettings.Local.json` (git-ignored — create your own):

```json
{
  "AzureAI": {
    "Endpoint": "https://<your-foundry-resource>.services.ai.azure.com/",
    "ApiKey": "<your-key>",
    "ChatModel": "gpt-4o"
  }
}
```

If `ApiKey` is empty, `AzureAIService` falls back to `DefaultAzureCredential`.

## Run

```powershell
cd "3. RpCCTranscriptAnalyze.Tools"
dotnet run
```

Expected output: a single JSON object with all 15 fields, including
`"call_date": "<today in UTC>"` produced by the tool call.

## Why this matters in the migration

Projects #2 and #3 share **the same extraction prompt**. The only difference
is that #3 needs information the model can't fabricate (today's date), and
that's exactly the inflection point where moving from "raw chat completion"
to "agent with tools" pays off. Future projects (#5 multi-agent workflow,
#7 retrieval-augmented agent, #8 web-search-equipped agent) will keep
building on this same MAF agent surface.
