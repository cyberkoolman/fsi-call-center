# 7. RpCCKbRAG — Knowledge-Base RAG (MAF Workflows + DevUI)

Agentic retrieval-augmented Q&A over the Contoso Tech-Support and Employee-Handbook PDFs, hosted in the Microsoft Agent Framework DevUI.

## What this replaces

The previous version was a single-shot console app built on **Semantic Kernel + Kernel Memory** that ingested two PDFs and answered one hardcoded question. It has been rewritten as a **.NET 9 + Microsoft Agent Framework 1.10** solution exposing **two RAG pipelines** as DevUI agents.

## Layout

```
7. RpCCKbRAG/
├── Documents/                          # PDFs preloaded at startup
│   ├── Contoso_Tech_Support_Guidelines.pdf
│   └── employee_handbook.pdf
├── RpCCKbRAG/                          # Library: workflow, executors, services
│   ├── Configuration/    AppSettings  (Azure OpenAI, pipeline, knowledge)
│   ├── Models/           RagState, ResearchPlan, WorkflowMessages, ...
│   ├── Services/         AzureAIService, VectorStore, DocumentLoader (PdfPig)
│   ├── Executors/        Planner, QueryRewriter, VectorSearch, Reranker,
│   │                     Distiller, Reflection, Policy, Synthesis, OneShotAnswer
│   └── Workflow/         AgenticRagWorkflow, OneShotRagWorkflow
└── RpCCKbRAG.Api/                      # ASP.NET Core host serving DevUI
    ├── Program.cs                      # Indexes PDFs, registers two agents
    ├── RagWorkflowChatClient.cs        # IChatClient → MAF Workflow bridge
    └── appsettings.json / .Local.json  # AOAI endpoint + deployments
```

## Two pipelines

Both pipelines are MAF `Workflow`s built with `WorkflowBuilder` and executed via
`InProcessExecution.RunStreamingAsync(...)`. They share the same in-memory
`VectorStore` (cosine sim over `text-embedding-3-large`).

### `KbRAG-OneShot` — fast, single-pass

```
UserQuery → QueryBridge → VectorSearch → OneShotAnswer
```

One vector search, one LLM call, citations. ~1–3 s per turn. Use for routine
look-ups ("What's the warranty escalation step?").

### `KbRAG-DeepThink` — agentic, multi-iteration

```
UserQuery → Planner → QueryRewriter → VectorSearch → Reranker
         → Distiller → Reflection → Policy ─┐
                                            ├─► (loop back to Planner)
                                            └─► Synthesis
```

The Planner decomposes the question into research steps. After each retrieval
round a Reflection executor scores coverage and gaps; a Policy executor decides
whether to keep iterating (max 10 iterations) or hand off to Synthesis. Use for
multi-hop questions that require combining facts across both PDFs.

The flow is purely **vector-only** — there is no web search component (this
application's knowledge is fixed to the indexed PDFs).

## Microsoft Agent Framework usage

| MAF surface | Where it is used |
|---|---|
| `Microsoft.Agents.AI.Workflows`   | Both pipelines are `Workflow` graphs. `Executor<TIn>` / `Executor<TIn,TOut>` types are the nodes; `WorkflowBuilder.AddEdge` wires them. |
| `IWorkflowContext`                | Shared scratchpad — every executor reads/writes `RagState` via `QueueStateUpdateAsync` / `ReadStateAsync` under scope `"rag"`. |
| `WorkflowOutputEvent`             | Synthesis / OneShotAnswer signal completion by yielding the final answer string; the API host pulls it out of `WatchStreamAsync()` and streams it to the UI. |
| `Microsoft.Agents.AI.Hosting.OpenAI` | Exposes the workflows as OpenAI-compatible `/v1/responses` agents — the same protocol Project #6 uses. |
| `Microsoft.Agents.AI.DevUI`       | Browser UI at `/devui` for chatting with both agents. |

The `RagWorkflowChatClient` is a thin `IChatClient` wrapper: each chat turn
calls `InProcessExecution.RunStreamingAsync(workflow, new UserQuery(text))`,
watches the event stream for the terminal `WorkflowOutputEvent`, and streams
the resulting string back to DevUI.

## Document indexing

`DocumentLoader` (custom, built on **UglyToad.PdfPig**) extracts each PDF page,
inserts page markers, chunks at ~500 tokens with 50-token overlap, and embeds
each chunk via `text-embedding-3-large`. Citations track the originating page
number so answers cite `[Document — page N]`.

Indexing happens **once at startup** in `Program.cs`. With the two seed PDFs
this produces ≈15 chunks total (6 + 9).

## Running locally

1. Edit `RpCCKbRAG.Api/appsettings.Local.json` with your Azure OpenAI endpoint,
   API key, chat deployment(s) (`gpt-4o`), and embedding deployment
   (`text-embedding-3-large`).

2. Run:

   ```cmd
   cd "7. RpCCKbRAG\RpCCKbRAG.Api"
   dotnet run
   ```

3. Open <http://localhost:8888/devui> and pick `KbRAG-OneShot` or
   `KbRAG-DeepThink` from the agent dropdown.

### Sample questions

| Pipeline | Question |
|---|---|
| OneShot   | What guidelines exist for handling angry customers? |
| OneShot   | What does the handbook say about remote work? |
| DeepThink | What is the escalation path for a damaged-package complaint, and which supervisor approval is required? |
| DeepThink | Compare the customer-complaint procedure with the employee grievance procedure — what do they have in common? |

## Configuration knobs

`RpCCKbRAG/Configuration/AppSettings.cs`:

* `AzureAI.ReasoningModel` — chat deployment used for Planner / Reflection / Synthesis (default `gpt-4o`)
* `AzureAI.FastModel`      — chat deployment used for query rewrite / strategy choice (default `gpt-4o`)
* `AzureAI.EmbeddingModel` — embedding deployment (default `text-embedding-3-large`)
* `Pipeline.MaxIterations` — agentic loop cap (default 3)
* `Pipeline.TopKRetrieval` / `TopKRerank` — retrieval/rerank breadth
* `Knowledge.DocumentsFolder` — folder relative to the API binary to index (default `Documents`)

## Reference

Inspired by `Agentic.RAG` (sibling repo) — the executor/workflow pattern is the
same; this project drops Tavily/web-search and is themed for the Contoso
call-center knowledge base.
