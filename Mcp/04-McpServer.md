# Option 4 — Run the MCP Server (stdio)

> **Goal:** Expose your own data to *any* AI app (Cursor, Claude Desktop, VS Code) through the **Model Context Protocol**, so their built-in assistants can search the `Experiences` table without you writing a chat UI.

Code: [`Mcp/McpServerRunner.cs`](McpServerRunner.cs) (the host) · [`Mcp/ExperienceMcpTools.cs`](ExperienceMcpTools.cs) (the tools)

> **Watch first:** the last part of
> [C# AI ML Tutorial for Beginners | Agents, RAG and MCP](https://www.youtube.com/watch?v=lEUPgdv0gY8)
> (Questpond) explains **what MCP is**: a standard way to expose your tools to AI products, shown with
> ChatGPT and GitHub Copilot calling a `send email` tool. The video deliberately doesn't show how to
> build the server. This option fills that gap with a working C# MCP server.
> Differences from the video are in [§11](#11-following-along-with-the-video).

---

## Table of Contents

1. [What MCP is](#1-what-mcp-is)
2. [Before you run it](#2-before-you-run-it)
3. [Two ways to start the server](#3-two-ways-to-start-the-server)
4. [The host: stdio and the stdout rule](#4-the-host-stdio-and-the-stdout-rule)
5. [The tools](#5-the-tools)
6. [How a tool call flows](#6-how-a-tool-call-flows)
7. [Connecting a client](#7-connecting-a-client)
8. [Testing with MCP Inspector](#8-testing-with-mcp-inspector)
9. [Troubleshooting](#9-troubleshooting)
10. [Practice exercises](#10-practice-exercises)
11. [Following along with the video](#11-following-along-with-the-video)

> **Settings for this option** live in `appsettings.json`: `OpenAI:EmbeddingModel` (used by
> `find_matching_experience`). Your API key and `ConnectionStrings:AimlDatabase` go in
> `appsettings.Local.json`. The server reads them from the build output folder.

---

## 1. What MCP is

In Chapter 1 you wrote tools and a loop that let *your* app's model call them. MCP flips that around: you publish tools once, and **any MCP-compatible AI client** can discover and call them.

```
┌──────────────┐   JSON-RPC over stdin/stdout   ┌───────────────────────┐
│ MCP client   │ ─────────────────────────────► │ AIMLAPP --mcp         │
│ (Cursor,     │   "tools/list", "tools/call"   │  find_matching_...    │──► OpenAI embeddings
│  Claude,     │ ◄───────────────────────────── │  list_experiences     │──► SQL Server
│  VS Code)    │          JSON results          │  get_experience_by_id │
└──────────────┘                                └───────────────────────┘
```

The client's model decides *when* to call a tool, just like function calling in Chapter 1. MCP standardizes the plumbing: how tools are listed, how they're described, and how calls and results are sent.

---

## 2. Before you run it

1. Create the table and run [option 2](../Services/02-SeedExperiences.md), so the tools have data.
2. Build in Release. Client configs use `--no-build`, so they run whatever was last built:

   ```bash
   dotnet build -c Release
   ```

The build copies `appsettings.json` and your `appsettings.Local.json` into `bin/Release/net10.0/`, which is where the server reads them from.

---

## 3. Two ways to start the server

| How | When to use it |
|---|---|
| `dotnet run -- --mcp` | **What MCP clients use.** No menu, nothing printed to stdout. |
| Menu option 4 | Quick check that the server starts. The menu text has already been printed to stdout, so don't point clients at this route. |

Both end up in the same `McpServerRunner.RunAsync`. Once started, the server sits silently waiting for JSON-RPC on stdin. That's normal; press `Ctrl+C` to stop it.

The `--mcp` check sits at the very top of `Program.cs`, before any `Console.WriteLine`:

```csharp
if (args.Contains("--mcp"))
{
    await McpServerRunner.RunAsync(settings.RequireOpenAiApiKey());
    return;
}
```

---

## 4. The host: stdio and the stdout rule

```csharp
var builder = Host.CreateEmptyApplicationBuilder(settings: null);

builder.Logging.AddConsole(o =>
{
    o.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlServer(AppDbContext.ConnectionString));

builder.Services.AddSingleton(_ => new EmbeddingClient(AppSettings.Current.OpenAI.EmbeddingModel, openAiApiKey));

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();
```

**The stdout rule:** with the stdio transport, **stdout is the protocol channel**. Every byte written there must be valid JSON-RPC. A single stray `Console.WriteLine("hello")` corrupts the stream and the client disconnects. That's why:

- `LogToStandardErrorThreshold = LogLevel.Trace` sends *all* log levels to **stderr**, which clients show as logs.
- `--mcp` skips the menu entirely.

The rest is ordinary .NET dependency injection: the database context and the embedding client are registered as services, and `WithToolsFromAssembly()` scans the project for classes marked `[McpServerToolType]`.

---

## 5. The tools

| Tool | Input | Returns | Calls OpenAI? |
|---|---|---|---|
| `find_matching_experience` | `experience` (free text) | Closest profile, its questions, and the cosine distance | Yes, one embedding |
| `list_experiences` | none | Count plus every profile | No |
| `get_experience_by_id` | `id` (int) | One profile, or an error | No |

A tool is just a static method with attributes:

```csharp
[McpServerTool(Name = "get_experience_by_id", ReadOnly = true),
 Description("Get a single experience profile by its numeric Id.")]
public static async Task<object> GetExperienceByIdAsync(
    [Description("The Id of the experience row to fetch.")] int id,
    AppDbContext db,
    CancellationToken cancellationToken = default)
```

Three things to notice:

1. **`Description` is the prompt.** The client's model reads these descriptions to decide which tool to call and what to pass. Vague descriptions lead to wrong tool choices. The video stresses the same point with its `SendEmail` tool: the model matches on the description, not the method name. It's also the lesson of tool schemas in Chapter 1.
2. **Only `id` is visible to the model.** `AppDbContext` and `CancellationToken` are filled in by the server from dependency injection for each call. They never appear in the tool's input schema.
3. **Errors are returned, not thrown.** A missing row returns `{ error = "No experience found with Id=..." }`. The model can read that and recover, for example by calling `list_experiences` to find valid ids. Chapter 1 §8 covers this pattern.

`find_matching_experience` runs the same vector search as [option 3](../Services/03-InterviewFlow.md), but also returns the distance so the calling model can judge how good the match is:

```csharp
Distance = EF.Functions.VectorDistance("cosine", e.ExpVector!.Value, userVector)
```

---

## 6. How a tool call flows

A user in Cursor types: *"Find interview topics for a mid-level React developer."*

```
1. Client ── tools/list ──► server       (once, at startup: names, descriptions, schemas)
2. Client's model picks find_matching_experience, experience = "mid-level React developer"
3. Client ── tools/call ──► server
4. Server: embed the text (OpenAI) → VECTOR_DISTANCE query (SQL Server)
5. Server ── JSON result ──► client
   { "id": 7, "experienceText": "Mid-level React developer with 3-5 years of experience",
     "questions": "React hooks, state management, TypeScript, ...", "cosineDistance": 0.21 }
6. Client's model writes the final answer using that result
```

Your server never talks to the client's model directly. It only answers tool calls, and the client owns the conversation.

---

## 7. Connecting a client

All clients need the same command. Use the absolute path to your `AIMLAPP.csproj`.

**Cursor**: the repo already ships a `.cursor/mcp.json` that uses `${workspaceFolder}`, so opening this folder in Cursor is enough. To use the server from other projects, add this to `~/.cursor/mcp.json` with your absolute path:

```json
{
  "mcpServers": {
    "aiml-experiences": {
      "command": "dotnet",
      "args": ["run", "--project", "/absolute/path/to/AIMLAPP/AIMLAPP.csproj",
               "-c", "Release", "--no-build", "--", "--mcp"]
    }
  }
}
```

**Claude Desktop**: the same `mcpServers` block in `claude_desktop_config.json` (on Windows, `%APPDATA%\Claude\`).

**VS Code**: `.vscode/mcp.json` uses a `servers` key and an explicit transport type:

```json
{
  "servers": {
    "aiml-experiences": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["run", "--project", "/absolute/path/to/AIMLAPP/AIMLAPP.csproj",
               "-c", "Release", "--no-build", "--", "--mcp"]
    }
  }
}
```

Then ask the client's assistant something like *"List the experience profiles"* or *"What should I ask a senior Azure engineer?"*

---

## 8. Testing with MCP Inspector

[MCP Inspector](https://github.com/modelcontextprotocol/inspector) is a browser UI for calling your tools by hand, with no AI client involved. It needs Node.js.

```bash
npx @modelcontextprotocol/inspector -- dotnet run --project AIMLAPP.csproj -c Release --no-build -- --mcp
```

The first `--` separates Inspector's own options from the server command. If your shell mangles the arguments, run `npx @modelcontextprotocol/inspector` on its own and type the command (`dotnet`) and arguments into its UI instead.

Click **Connect**, open **Tools**, run **List Tools**, then call `list_experiences`. It's the fastest way to check that the server works before debugging a client config.

---

## 9. Troubleshooting

| Symptom | Likely cause |
|---|---|
| Client shows the server as failed immediately | No Release build yet, or `appsettings.Local.json` missing from `bin/Release/net10.0/`. Run `dotnet build -c Release`. |
| Error about a missing API key or connection string | Secrets aren't in `appsettings.Local.json` (or env vars) for the Release output. |
| Your code changes don't show up | `--no-build` runs the last build. Rebuild in Release. |
| Client connects, then disconnects | Something wrote to stdout. Look for a `Console.WriteLine` on the `--mcp` path. |
| `list_experiences` returns `count: 0` | The table is empty. Run option 2. |

---

## 10. Practice exercises

1. **Add a tool.** Write `search_by_keyword(string keyword)` that returns profiles whose `Questions` contain the keyword. Rebuild in Release and call it from MCP Inspector.
2. **Mark it read-only.** `find_matching_experience` doesn't change any data, but it lacks `ReadOnly = true`. Add it and see whether your client treats it differently, for example by asking for approval less often.
3. **Return the top 3.** Change `find_matching_experience` to return the three closest profiles with their distances, and let the client's model choose.
4. **Break the stdout rule on purpose.** Add `Console.WriteLine("hi")` inside a tool, rebuild, call it, and watch the client fail. Then remove it. You'll recognize this bug instantly in the future.

---

## 11. Following along with the video

| In the video | In this repo |
|---|---|
| Explains the concept and shows a finished server. How to build it is left to a separate video | The full server is here: `McpServerRunner.cs` (about 30 lines) and `ExperienceMcpTools.cs` |
| The tool is `send email`, taken from the tool-calling demo | The tools query the `Experiences` table from options 2–3, so MCP clients get the same RAG data |
| The server is reached over **HTTP**, which remote products like ChatGPT need | The server uses **stdio**, which local clients (Cursor, Claude Desktop, VS Code) launch as a child process |
| ChatGPT and Copilot ask "allow this tool?" before calling it | Clients do the same here. Read-only hints (`ReadOnly = true`) tell them which tools are safe (Exercise 2) |

**Going further:** to reach this server from a remote product like ChatGPT, switch the transport from stdio to HTTP. With the C# SDK, that means hosting it in ASP.NET Core with the `ModelContextProtocol.AspNetCore` package and `.WithHttpTransport()` instead of `.WithStdioServerTransport()`. The tool classes don't change.
