# 8. RpContactCenterApi — Unified Contact Center API (MAF + DevUI)

The unified entry point for the Contoso Contact Center. A single **Microsoft
Agent Framework** agent named **`ContactCenter`** handles both kinds of
questions agents and managers ask:

* **Caller-data analytics** (NL → SQL against the `Call_Center` database)
* **Policy / knowledge-base questions** (vector search over the Contoso PDFs)

The agent auto-routes each question to the right tool — same end-result as the
original Semantic Kernel + Kernel Memory implementation, on **.NET 9 + MAF
1.10**.

## What this replaces

The original project combined three Semantic Kernel plugins behind a single
`/Question` controller:

* `NlpToSqlPlugin` (prompt-based NL → SQL)
* `QueryDbPlugin` (native SQL execution)
* `RAGPlugin` (Kernel Memory document search)

These have been collapsed into **one MAF agent with two `[Description]`-annotated
tool methods**, hosted in DevUI.

## Architecture

```
                       ┌─────────────────────────────────┐
   user question  ───► │   ContactCenter MAF agent        │
                       │   (gpt-4o, auto tool-calling)    │
                       └──────────────┬──────────────────┘
                                      │
              ┌───────────────────────┴──────────────────────┐
              ▼                                              ▼
   ┌────────────────────────┐                  ┌─────────────────────────────┐
   │ ExecuteSqlAsync        │                  │ SearchKnowledgeBaseAsync    │
   │  • SELECT-only guard   │                  │  • text-embedding-3-large   │
   │  • SQL Server / EF-less│                  │  • In-memory cosine search  │
   │  • Returns rows JSON   │                  │  • Returns top-K chunks     │
   └────────────────────────┘                  └─────────────────────────────┘
              │                                              │
              ▼                                              ▼
   `Call_Center.CustomerIssues`                    `Documents/*.pdf`
```

The agent's prompt (`Prompts/ContactCenterAgent.md`) explains the two tools,
when to call each, and the SQL schema. MAF auto-tool-calling handles the
LLM ↔ tool round-trips; the agent always finishes with a synthesised answer
plus citations (for KB) or numbers + derivation note (for SQL).

## Layout

```
8. RpContactCenterApi/
├── Documents/                       # PDFs preloaded at startup
│   ├── Contoso_Tech_Support_Guidelines.pdf
│   └── employee_handbook.pdf
├── Configuration/AppSettings.cs    # AzureAI, ConnectionStrings, Pipeline, Knowledge
├── Models/RagDocument.cs           # Chunk record (source, section, content, embedding)
├── Services/
│   ├── AzureAIChatClientFactory.cs # IChatClient over Azure OpenAI
│   ├── AzureAIService.cs           # Embeddings client
│   ├── DocumentLoader.cs           # PdfPig-based PDF chunker
│   └── VectorStore.cs              # In-memory cosine-similarity search
├── Tools/
│   ├── QueryDbTool.cs              # ExecuteSqlAsync (SELECT-only guard)
│   └── KnowledgeBaseTool.cs        # SearchKnowledgeBaseAsync (vector top-K)
├── Prompts/ContactCenterAgent.md   # Unified agent instructions
├── Program.cs                      # Indexes PDFs, registers ContactCenter agent
├── RpContactCenterApi.csproj
├── appsettings.json                # Template (real values in .Local.json)
└── .gitignore
```

## Microsoft Agent Framework usage

| Surface | Where |
|---|---|
| `Microsoft.Agents.AI.Hosting` (`AddAIAgent`) | One agent named `ContactCenter` registered with the unified prompt. |
| `WithAITool(...)` + `AIFunctionFactory.Create(...)` | Each tool method (`ExecuteSqlAsync`, `SearchKnowledgeBaseAsync`) registered as an `AIFunction` keyed to the agent. |
| `Microsoft.Agents.AI.Hosting.OpenAI` | Exposes the agent as an OpenAI-compatible `/v1/responses` endpoint. |
| `Microsoft.Agents.AI.DevUI` | Browser UI at `/devui` for chatting with the agent. |
| `Microsoft.Extensions.AI` (`IChatClient`) | The chat client over Azure OpenAI (`AzureAIChatClientFactory`). |

The unified agent is a **single `ChatClientAgent`** — no `Workflow`, no
explicit graph. Routing between SQL and KB is done by the LLM itself based on
the prompt and tool descriptions.

## Indexing

PDFs in `Documents/` are read once at startup with **UglyToad.PdfPig**, chunked
at ≈500 tokens / 50 overlap, embedded with `text-embedding-3-large`, and held
in an in-memory `VectorStore` (≈15 chunks for the two seed PDFs). Citations
preserve the originating page number.

## Running locally

1. Edit `appsettings.Local.json` with your Azure OpenAI endpoint, API key,
   chat deployment (`gpt-4o`), embedding deployment
   (`text-embedding-3-large`), and a SQL Server connection string targeting
   the `Call_Center` database.

2. Run:

   ```cmd
   cd "8. RpContactCenterApi"
   dotnet run
   ```

3. Open <http://localhost:8888/devui>, pick **`ContactCenter`** from the agent
   dropdown.

### Sample questions

| Type | Question | Expected route |
|---|---|---|
| KB     | What guidelines exist for handling angry customers?                  | `SearchKnowledgeBaseAsync` |
| KB     | What is the escalation path for a damaged-package complaint?         | `SearchKnowledgeBaseAsync` |
| KB     | What does the employee handbook say about workplace safety?          | `SearchKnowledgeBaseAsync` |
| SQL    | How many support calls were made in the last 6 months?               | `ExecuteSqlAsync` |
| SQL    | Which employee resolved the most calls last quarter?                 | `ExecuteSqlAsync` |
| SQL    | Average satisfaction-score change for damaged-package issues?        | `ExecuteSqlAsync` |
| Mixed  | What is our angry-customer procedure, and how often did it apply?    | both tools |

## Configuration

`Configuration/AppSettings.cs`:

* `AzureAI.ChatModel` / `EmbeddingModel` — Azure OpenAI deployment names
* `ConnectionStrings.CallCenter` — SQL Server connection string
* `Pipeline.ChunkSize` / `ChunkOverlap` / `TopK` — chunking & retrieval tuning
* `Knowledge.DocumentsFolder` — PDF folder relative to the binary (default `Documents`)
