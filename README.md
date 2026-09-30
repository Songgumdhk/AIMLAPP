# AIMLAPP: AI Engineering in .NET

A hands-on .NET 10 console app for learning AI engineering. Each chapter pairs a written guide (`Learning/NN-*.md`) with a runnable demo, and the chapters build on each other: function calling first, then Semantic Kernel, advanced RAG, agentic workflows, evaluation, observability, production hardening, and local models.

The app also includes a small end-to-end sample: an interview-question service that stores embeddings in SQL Server's `VECTOR` type, finds the closest profile with cosine distance, and exposes the same data to AI clients through an MCP server.

> **New to AI in C#? Start with the video.** The sample app (menu options 1–4) follows
> [C# AI ML Tutorial for Beginners | Agents, RAG and MCP](https://www.youtube.com/watch?v=lEUPgdv0gY8)
> by Shivprasad Koirala ([Questpond](https://www.youtube.com/@questpondvideos)): agents, vectors and
> embeddings, RAG with SQL Server, sessions, tool calling, and MCP. Watch it, then run options 1–4 with
> their guides. Each guide lists where the code here differs from the video.

---

## Contents

- [What's inside](#whats-inside)
- [Requirements](#requirements)
- [Getting started](#getting-started)
- [Configuration](#configuration)
- [Database setup (options 2–4)](#database-setup-options-24)
- [Running as an MCP server](#running-as-an-mcp-server)
- [Project structure](#project-structure)
- [Learning roadmap](#learning-roadmap)
- [Security notes](#security-notes)
- [License](#license)

---

## What's inside

Run the app and pick a number from the menu:

| Option | What it does | Guide |
|---|---|---|
| 1 | Chat with a simple agent (Microsoft Agent Framework) | [`01-ChatWithAgent.md`](Services/01-ChatWithAgent.md) |
| 2 | Seed the `Experiences` table with embedded profiles | [`02-SeedExperiences.md`](Services/02-SeedExperiences.md) |
| 3 | Interview flow: embed your description, vector-search SQL Server, generate questions | [`03-InterviewFlow.md`](Services/03-InterviewFlow.md) |
| 4 | Run the MCP server over stdio | [`04-McpServer.md`](Mcp/04-McpServer.md) |
| 5 | Chapter 1: Function calling without a framework | [`01-FunctionCalling.md`](Learning/01-FunctionCalling.md) |
| 6 | Chapter 2: Semantic Kernel plugins and filters | [`02-SemanticKernel.md`](Learning/02-SemanticKernel.md) |
| 7 | Chapter 3: Advanced RAG (chunking, hybrid search, RRF, reranking, query rewriting) | [`03-AdvancedRAG.md`](Learning/03-AdvancedRAG.md) |
| 8 | Chapter 4: Agentic workflows (planning, state, memory, human-in-the-loop, multi-agent) | [`04-AgenticWorkflows.md`](Learning/04-AgenticWorkflows.md) |
| 9 | Chapter 5: AI evaluation (retrieval metrics, hallucination checks, LLM-as-judge) | [`05-AIEvaluation.md`](Learning/05-AIEvaluation.md) ([short version](Learning/05-AIEvaluation-Simple.md)) |
| 10 | Chapter 6: Observability (tracing, token usage, latency, cost) | [`06-Observability.md`](Learning/06-Observability.md) |
| 11 | Chapter 7: Production AI (prompt injection, guardrails, caching, rate limiting, deployment) | [`07-ProductionAI.md`](Learning/07-ProductionAI.md) |
| 12 | Chapter 8: Local AI (Ollama, ONNX-style inference, local embeddings) | [`08-LocalAI.md`](Learning/08-LocalAI.md) |

Options 1–4 follow the [Agents, RAG and MCP video](https://www.youtube.com/watch?v=lEUPgdv0gY8) in order: first agent, embeddings, RAG, then MCP. Options 5–12 go deeper, one topic per chapter.

Options 1 and 5–11 need only an OpenAI API key. Options 2–4 also need SQL Server. Option 12 needs neither; Ollama is optional.

---

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- An [OpenAI API key](https://platform.openai.com/api-keys) for options 1–11
- SQL Server 2025 or Azure SQL Database (anything with the `VECTOR` data type) for options 2–4
- Optional: [Ollama](https://ollama.com) for the live parts of Chapter 8

Main packages: `OpenAI`, `Microsoft.Extensions.AI`, `Microsoft.Agents.AI`, `Microsoft.SemanticKernel`, `Microsoft.EntityFrameworkCore.SqlServer`, `ModelContextProtocol`.

---

## Getting started

```bash
git clone https://github.com/Songgumdhk/AIMLAPP.git
cd AIMLAPP
```

Create your local secrets file from the template. This file is gitignored.

```bash
# macOS / Linux
cp appsettings.Local.example.json appsettings.Local.json

# Windows (PowerShell)
Copy-Item appsettings.Local.example.json appsettings.Local.json
```

Fill in your values:

```json
{
  "ConnectionStrings": {
    "AimlDatabase": "Server=YOUR_SERVER;Database=aimlDatabase;User Id=YOUR_USER;Password=YOUR_PASSWORD;TrustServerCertificate=True;"
  },
  "OpenAI": {
    "ApiKey": "sk-your-openai-key"
  }
}
```

Then run:

```bash
dotnet run
```

If you only want the Local AI chapter, you can skip the secrets file and pick option **12**.

---

## Configuration

All settings live in [`appsettings.json`](appsettings.json) and are loaded through the typed `AppSettings` class in [`Configuration/AppSettings.cs`](Configuration/AppSettings.cs). Every AI-related value (models, embedding model and dimensions, temperature, token limits, retrieval and agent limits, pricing) comes from there, so you can change models without touching code.

Sources are applied in this order, and later ones win:

1. `appsettings.json` (committed, no secrets)
2. `appsettings.Local.json` (gitignored, your secrets and personal overrides)
3. Environment variables, using `__` for nesting, for example `OpenAI__ChatModel=gpt-4o-mini` or `ConnectionStrings__AimlDatabase=...`
4. `OPENAI_API_KEY`, which is also accepted for the API key

### Sections

| Section | What it controls |
|---|---|
| `ConnectionStrings:AimlDatabase` | SQL Server connection for options 2–4 (keep it in `appsettings.Local.json`) |
| `OpenAI` | `ApiKey`, `ChatModel`, `SmallChatModel`, `EmbeddingModel`, `EmbeddingDimensions`, `Temperature` |
| `Ollama` | `BaseUrl`, `ChatModel`, `EmbeddingModel`, `RequestTimeoutSeconds` |
| `Pricing` | Per-model input/output price per 1M tokens, used by the Observability cost view |
| `FunctionCalling` | `MaxTurns` safety cap for the tool loop |
| `Rag` | `TopK`, `RerankCandidates`, `RrfK`, `QueryRewriteCount`, chunk size, overlap, sentences per chunk |
| `AgenticWorkflows` | `ReviewPassScore`, `MaxRevisions`, `MaxNodeVisits`, `MaxAgentHops` |
| `Observability` | Output-token caps for the traced calls |
| `ProductionAi` | `MaxOutputTokens`, `GuardrailsEnabled`, `RequestsPerMinute`, `CacheMaxEntries`, `LogPrompts` |

A few things to know:

- `SmallChatModel` is used by the Observability and Production AI chapters to keep repeated runs cheap. Everything else uses `ChatModel`.
- If you change `EmbeddingModel`, update `EmbeddingDimensions` to match. The SQL column is `VECTOR(N)`, so you'll also need to recreate the table and re-seed.
- If you switch to a model that isn't listed under `Pricing`, add a price entry or the Observability cost view will stop with an error telling you to.
- The Production AI chapter checks its own config at startup and refuses to run if guardrails are off, the rate limit is zero, or prompt logging is on.

---

## Database setup (options 2–4)

Create the table once. The vector size must match `OpenAI:EmbeddingDimensions` (1536 for `text-embedding-3-small`).

```sql
CREATE TABLE Experiences (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    Experience  NVARCHAR(MAX) NOT NULL,
    ExpVector   VECTOR(1536)  NULL,
    Questions   NVARCHAR(MAX) NULL
);
```

Then:

1. Run option **2** to embed and insert the sample profiles.
2. Run option **3** and describe yourself, for example "senior .NET developer with 6 years". The app embeds your text, finds the closest profile with `VECTOR_DISTANCE('cosine', ...)`, and generates five interview questions.

The step-by-step guides for these options are [`02-SeedExperiences.md`](Services/02-SeedExperiences.md) and [`03-InterviewFlow.md`](Services/03-InterviewFlow.md).

---

## Running as an MCP server

The app can run as a [Model Context Protocol](https://modelcontextprotocol.io) server over stdio, which lets MCP clients such as Cursor, Claude Desktop, or VS Code query the `Experiences` table.

| Tool | Description |
|---|---|
| `find_matching_experience` | Embeds a free-form description and returns the closest profile and its questions |
| `list_experiences` | Lists every seeded profile |
| `get_experience_by_id` | Returns one profile by id |

Build in Release first. If you open this folder in Cursor, the included [`.cursor/mcp.json`](.cursor/mcp.json) picks the server up automatically. For other clients, point them at the project:

```bash
dotnet build -c Release
```

```json
{
  "mcpServers": {
    "aiml-experiences": {
      "command": "dotnet",
      "args": ["run", "--project", "/absolute/path/to/AIMLAPP/AIMLAPP.csproj", "-c", "Release", "--no-build", "--", "--mcp"]
    }
  }
}
```

In MCP mode the app writes nothing to stdout except JSON-RPC, and all logs go to stderr. It reads the same `appsettings*.json` files from the build output folder. See [`04-McpServer.md`](Mcp/04-McpServer.md) for how it works, client setup for Cursor, Claude Desktop and VS Code, testing with MCP Inspector, and troubleshooting.

---

## Project structure

```
AIMLAPP/
├── Program.cs                  Menu and entry point (--mcp for MCP mode)
├── appsettings.json            All AI settings (committed, no secrets)
├── appsettings.Local.example.json  Template for your gitignored secrets file
├── Configuration/AppSettings.cs    Typed settings and loader
├── ConsoleUi/AppBanner.cs      ASCII banner and startup menu
├── Data/AppDbContext.cs        EF Core context with a SQL Server VECTOR column
├── Models/Experience.cs        Experiences table entity
├── Services/                   Agent chat, seeder, interview flow + guides (options 1–3)
├── Mcp/                        MCP server host, tools + guide (option 4)
└── Learning/
    ├── 01-FunctionCalling.md … 08-LocalAI.md   Chapter guides
    ├── FunctionCallingDemo/    Chapter 1
    ├── SemanticKernelDemo/     Chapter 2
    ├── AdvancedRagDemo/        Chapter 3
    ├── AgenticWorkflowsDemo/   Chapter 4
    ├── AiEvaluationDemo/       Chapter 5
    ├── ObservabilityDemo/      Chapter 6
    ├── ProductionAiDemo/       Chapter 7
    └── LocalAiDemo/            Chapter 8
```

Each demo folder has an entry-point class, `<Name>Demo.cs`, with a header comment mapping its subfolders to sections of the chapter guide.

---

## Learning roadmap

Start with the sample app (menu options 1–4). It covers the basics every chapter assumes: calling an LLM, what an agent is, embeddings, vector search, basic RAG, and MCP. Then work through the chapters (options 5–12) in order.

```
START HERE: the sample app (options 1–4, follows the video)

Option 1. Chat with agent ─────── your first agent: model, instructions, streaming
        │
Option 2. Seed Experiences ────── embeddings, cosine similarity, SQL Server VECTOR column
        │
Option 3. Run interview ───────── basic RAG: embed → vector search → grounded agent
        │
Option 4. MCP server ──────────── expose your data as tools to Cursor, Claude, VS Code
        │
        ▼
THE CHAPTERS (options 5–12)

1. Function Calling / Tool Calling
        │
2. Semantic Kernel
        │
3. Advanced RAG ──────── chunking, hybrid search, reranking, metadata filtering, query rewriting
        │
4. Agentic Workflows ─── planning, state, memory, human-in-the-loop, multi-agent
        │
5. AI Evaluation ─────── retrieval metrics, RAG evaluation, hallucination detection, LLM-as-judge
        │
6. Observability ─────── tracing, token usage, latency, cost
        │
7. Production AI ─────── security, guardrails, caching, rate limiting, deployment
        │
8. Local AI ──────────── Ollama, ONNX, local embeddings, local LLMs
```

### Start here: options 1–4

A small interview-question app built step by step, following the [Agents, RAG and MCP video](https://www.youtube.com/watch?v=lEUPgdv0gY8):

- **Option 1, Chat with agent** ([guide](Services/01-ChatWithAgent.md)): the smallest useful agent, built from one model, one instruction and streamed output. It also shows why agents are stateless and why they give generic answers without your own data.
- **Option 2, Seed Experiences table** ([guide](Services/02-SeedExperiences.md)): turn profile descriptions into embeddings and store them in SQL Server's `VECTOR` column.
- **Option 3, Run interview** ([guide](Services/03-InterviewFlow.md)): Retrieve → Augment → Generate. Embed the user's description, find the closest profile by cosine distance, and have the agent write questions grounded in it.
- **Option 4, Run MCP server** ([guide](Mcp/04-McpServer.md)): publish the same data as MCP tools, so assistants in Cursor, Claude Desktop or VS Code can query it.

Options 2–4 need SQL Server; see [Database setup](#database-setup-options-24).

### 1. Function calling

Learn the raw mechanics before any framework hides them: tool schemas, how the model picks a tool, arguments, multiple and parallel tool calls, tool errors, retries, and human approval for dangerous actions. The demo runs the tool loop by hand with a `MaxTurns` safety cap.

### 2. Semantic Kernel

The same tool loop, now handled by a framework built for .NET. Plugins are plain C# classes, filters act as middleware (logging, approval), and `FunctionChoiceBehavior.Auto()` replaces the hand-written loop from Chapter 1. Switching providers is a one-line change.

```
Your .NET app → Semantic Kernel → LLM → Plugins → Database / APIs / RAG / MCP
```

### 3. Advanced RAG

Go past "embed, cosine similarity, stuff into prompt". The demo lets you compare chunking strategies and eight retrieval modes over one corpus:

```
Query → Rewrite → Hybrid search (vector + keyword, merged with RRF) → Metadata filter → LLM rerank → Top chunks → Answer with citations
```

### 4. Agentic workflows

How do you design an agent system that is reliable, not just clever? The demo covers planning, explicit state and a state machine, conditional routing, persisted runs, memory, a human approval gate, and a supervisor coordinating specialist agents. Hard caps on revisions, node visits, and agent hops stop runaway loops.

Worth reading alongside: LangGraph's concepts translate directly to .NET, even if you never write Python in production.

### 5. AI evaluation

If you can't measure it, you can't tell whether a change helped. The demo freezes a labeled question set and scores three retrievers (naive, keyword, vector) with precision, recall, and MRR. It then checks answers for groundedness, computes a claim-level hallucination rate, and runs an LLM judge, including a pass that swaps answer order to expose position bias.

```
Question → Were the right documents retrieved? → Did the LLM use them? → Was the answer actually supported?
```

### 6. Observability

One handbook question is recorded as a trace: embed, retrieve, tool call, chat completion. The demo prints the span tree, input and output tokens per step, which step took the time, and what it cost. Comparing a short prompt with a padded one shows why stuffing context moves the bill.

### 7. Production AI

What changes once real users, real documents, and a real bill are involved:

- **Security:** direct and indirect prompt injection, data leakage, tool authorization, PII redaction, secrets management
- **Guardrails:** check the question before the model sees it, and the answer before the user does
- **Caching:** the same question shouldn't pay for a second model call; bound the cache by size and time, and never cache personal data
- **Rate limiting:** stop a burst before it becomes a bill
- **Deployment:** read config from the environment, refuse to start with unsafe settings, never print the API key

Options 1–5 in the demo don't call the model at all. Option 6 runs the full guarded pipeline and can make one real call.

### 8. Local AI

Run models on your own machine: Ollama, GGUF and ONNX formats, quantization (Q4), CPU vs GPU inference, local embeddings, and local RAG. The demo runs a tiny model from a file, compares bag-of-words vectors with `nomic-embed-text`, and chats with a local `llama3.2` if Ollama is running.

```bash
ollama pull llama3.2
ollama pull nomic-embed-text
```

### What's next: multi-agent systems

Once single-agent workflows feel solid, look at supervisor agents, specialized agents, shared state, handoffs, parallel agents, and evaluating agent systems. Chapter 4 includes a small supervisor example as a starting point.

### Further learning

- [*C# AI ML Tutorial for Beginners | Agents, RAG and MCP*](https://www.youtube.com/watch?v=lEUPgdv0gY8) (YouTube, Questpond): the walkthrough behind options 1–4. It covers models vs LLMs, Microsoft Agent Framework agents, embeddings and cosine similarity, RAG with SQL Server 2025, sessions, tool calling, and MCP.
- [*Forward Deployed Engineer Full Course | GenAI Full Course*](https://www.youtube.com/watch?v=kBM5UXRbo3U&list=PLWSlr9idVBc0) (YouTube playlist, Coder Army): a multi-part GenAI course, framed around the Forward Deployed Engineer role. Good for broadening beyond .NET.
- [*Attention Is All You Need*](https://proceedings.neurips.cc/paper_files/paper/2017/file/3f5ee243547dee91fbd053c1c4a845aa-Paper.pdf) (Vaswani et al., NeurIPS 2017, research paper): the paper that introduced the **Transformer**, the architecture behind GPT, Claude, Gemini, Llama and modern embedding models. Read it to see what's inside the models every chapter calls. Sections 3.2 (attention) and 3.5 (positional encoding) are the core ideas.
- *Develop Single Agent .NET Applications Using Semantic Kernel* (Udemy): Semantic Kernel, RAG, MCP, plugins, local models with ONNX, Ollama, and Hugging Face
- *Semantic Kernel for .NET: Plugins, Agents & RAG* (Udemy): plugins, automatic function invocation, chat history, embeddings, RAG
- *Agentic AI Engineering with LangChain & LangGraph* (Udemy): LangGraph, MCP, memory, tool calling, and production agent patterns

---

## Security notes

- **Never commit secrets.** API keys and connection strings belong in `appsettings.Local.json` (gitignored) or environment variables. `appsettings.json` ships with empty values on purpose.
- **Watch what you spend.** Most demos call OpenAI. The function-calling loop, agent workflows, and RAG pipeline all have caps configured in `appsettings.json`, but a long session still costs money.
- **The tools are fake.** Chapter 1 and 2 tools such as "delete record" and "send email" only print what they would do. They exist to teach approval flows.
- **Demo data is fictional.** The "Acme" handbook, employees, and emails in the sample corpora are made up.

---

## License

[MIT](LICENSE)
