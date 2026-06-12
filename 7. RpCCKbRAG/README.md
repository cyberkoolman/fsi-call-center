# 7. RpCCKbRAG — ContosoKB (simplest RAG with MAF Workflows + DevUI)

A minimal Retrieval-Augmented-Generation agent over the Contoso Tech-Support
Guidelines and Employee Handbook PDFs, hosted as a single DevUI agent named
**`ContosoKB`**.

## What this replaces

The original project was a console app built on **Semantic Kernel + Kernel
Memory** that ingested two PDFs and answered one hardcoded question. It is now
a **.NET 9 + Microsoft Agent Framework 1.10** workflow.

## The pipeline

The simplest possible RAG topology — two executors, one edge:

```
UserQuery ──► [VectorSearch] ──► [Answer] ──► output
```

* **VectorSearch** — embeds the user query with `text-embedding-3-large`,
  pulls the top-K most similar chunks from the in-memory `VectorStore`.
* **Answer** — formats those chunks into a grounded prompt, calls the chat
  model, returns the answer with inline `[Source — Section]` citations.

No planner, no rewriter, no reranker, no reflection, no loop.

## Layout

```
7. RpCCKbRAG/
├── Documents/                          # PDFs preloaded at startup
│   ├── Contoso_Tech_Support_Guidelines.pdf
│   └── employee_handbook.pdf
├── RpCCKbRAG/                          # Library
│   ├── Configuration/  AppSettings (AzureAI, Pipeline, Knowledge)
│   ├── Models/         RagDocument, WorkflowMessages
│   ├── Services/       AzureAIService, VectorStore, DocumentLoader (PdfPig)
│   ├── Executors/      VectorSearchExecutor, AnswerExecutor
│   └── Workflow/       ContosoKbWorkflow
└── RpCCKbRAG.Api/                      # ASP.NET Core host serving DevUI
    ├── Program.cs                      # Indexes PDFs, registers ContosoKB agent
    ├── RagWorkflowChatClient.cs        # IChatClient → MAF Workflow bridge
    └── appsettings.json / .Local.json  # AOAI endpoint + deployments
```

## Microsoft Agent Framework usage

| Surface | Where |
|---|---|
| `Microsoft.Agents.AI.Workflows` | The pipeline is a `Workflow` built with `WorkflowBuilder`. `Executor<TIn>` types are the nodes. |
| `WorkflowOutputEvent` | `AnswerExecutor` calls `YieldOutputAsync` with the final answer string. |
| `Microsoft.Agents.AI.Hosting.OpenAI` | Exposes the workflow as an OpenAI-compatible `/v1/responses` agent. |
| `Microsoft.Agents.AI.DevUI` | Browser UI at `/devui` for chatting with `ContosoKB`. |

`RagWorkflowChatClient` is a thin `IChatClient` wrapper: each chat turn calls
`InProcessExecution.RunStreamingAsync(workflow, new UserQuery(text))`, watches
the event stream for the terminal `WorkflowOutputEvent`, and streams the
answer back to DevUI.

## Document indexing

`DocumentLoader` (built on **UglyToad.PdfPig**) extracts each PDF page,
inserts page markers, chunks at ~500 tokens with 50-token overlap, and embeds
each chunk via `text-embedding-3-large`. Citations track the originating page
number so answers cite `[Document — page N]`.

Indexing happens **once at startup** in `Program.cs` (≈15 chunks across the
two seed PDFs).

## Running locally

1. Edit `RpCCKbRAG.Api/appsettings.Local.json` with your Azure OpenAI endpoint,
   API key, chat deployment (`gpt-4o`), and embedding deployment
   (`text-embedding-3-large`).

2. Run:

   ```cmd
   cd "7. RpCCKbRAG\RpCCKbRAG.Api"
   dotnet run
   ```

3. Open <http://localhost:8888/devui>, pick **`ContosoKB`** from the agent
   dropdown, and ask questions.

### Sample questions

* What guidelines exist for handling angry customers?
* What does Contoso say about de-escalation techniques?
* What is the warranty policy for damaged products?
* What does the employee handbook say about remote work?
* What are the working hours described in the handbook?
* What is the escalation path for a damaged-package complaint?

## Configuration

`RpCCKbRAG/Configuration/AppSettings.cs`:

* `AzureAI.ChatModel` — chat deployment (default `gpt-4o`)
* `AzureAI.EmbeddingModel` — embedding deployment (default `text-embedding-3-large`)
* `Pipeline.ChunkSize` / `ChunkOverlap` — chunking tuning
* `Pipeline.TopK` — number of chunks retrieved per query (default 5)
* `Knowledge.DocumentsFolder` — folder relative to the API binary to index (default `Documents`)
