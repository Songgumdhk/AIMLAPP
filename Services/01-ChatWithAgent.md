# Option 1 — Chat with an Agent

> **Goal:** Build the smallest useful AI agent in .NET: one model, one instruction, streamed output. It's the "hello world" that every later option builds on.

Code: [`Services/AgentChat.cs`](AgentChat.cs)

> **Watch first:** options 1–4 follow the video
> [C# AI ML Tutorial for Beginners | Agents, RAG and MCP](https://www.youtube.com/watch?v=lEUPgdv0gY8)
> by Shivprasad Koirala ([Questpond](https://www.youtube.com/@questpondvideos)). This option is the
> video's **mock interview agent** section: models vs LLMs, goal / reason / act, the NuGet packages, and
> `IChatClient`. The agent's instruction text here comes straight from that demo. Watch that part, then
> use this guide to go deeper. Differences from the video are listed in [§8](#8-following-along-with-the-video).

---

## Table of Contents

1. [What you'll see](#1-what-youll-see)
2. [How it works](#2-how-it-works)
3. [The three building blocks](#3-the-three-building-blocks)
4. [Why the agent forgets you](#4-why-the-agent-forgets-you)
5. [What instructions can and cannot do](#5-what-instructions-can-and-cannot-do)
6. [How this leads to options 2–4](#6-how-this-leads-to-options-24)
7. [Practice exercises](#7-practice-exercises)
8. [Following along with the video](#8-following-along-with-the-video)

> **Settings for this option** live in `appsettings.json`: `OpenAI:ChatModel` picks the model.
> Your API key goes in `appsettings.Local.json`. No database is needed.
> See the README's Configuration section.

---

## 1. What you'll see

```
dotnet run
Selection: 1
Enter who you are (type 'exit' to quit):
Senior .NET developer, 6 years, mostly ASP.NET Core and Azure

User: Senior .NET developer, 6 years, mostly ASP.NET Core and Azure

Agent: 1. How would you ...
       2. ...
       3. ...
------------------------------------------------------------
```

The answer appears word by word because it is **streamed**. Type `exit` to quit.

---

## 2. How it works

```
You type a description
        │
        ▼
AIAgent  ──(instructions + your message)──►  OpenAI chat model
        ◄──────────── streamed tokens ─────────────┘
        │
        ▼
Console.Write(update) for each chunk
```

One request per turn. No tools, no database, no memory.

---

## 3. The three building blocks

### 3.1 `IChatClient`: the provider-neutral interface

```csharp
IChatClient chatClient = new ChatClient(AppSettings.Current.OpenAI.ChatModel, openAiApiKey).AsIChatClient();
```

`ChatClient` is the OpenAI SDK client. `.AsIChatClient()` wraps it in `Microsoft.Extensions.AI.IChatClient`, a common interface that Azure OpenAI, Ollama and other providers also implement. Code written against `IChatClient` doesn't care which provider is behind it.

### 3.2 `AIAgent`: a model plus instructions

```csharp
AIAgent agent = chatClient.AsAIAgent(
    name: "MyAgent",
    instructions: "Send only 3 interview questions as per the skill put by the developer ...");
```

`AsAIAgent` comes from the **Microsoft Agent Framework** (`Microsoft.Agents.AI`, often shortened to MAF). The video describes an agent as **Goal, Reason, Act**: the instructions are the *goal*, the LLM is the *reasoning*, and returning the questions is the *act*. The `instructions` become the **system prompt**, which is sent with every request and tells the model how to behave. The `name` identifies the agent in logs and multi-agent setups.

An agent here is deliberately simple. Later chapters add the pieces that make agents powerful: tools (Chapter 1), plugins (Chapter 2), planning and state (Chapter 4).

### 3.3 Streaming

```csharp
await foreach (var update in agent.RunStreamingAsync(userPrompt))
{
    Console.Write(update);
}
```

`RunStreamingAsync` returns tokens as the model generates them. The total time is the same as a non-streaming call, but the user sees the first words almost immediately, so it *feels* much faster. Use `RunAsync` instead when you need the whole answer before doing anything with it, for example to parse JSON.

---

## 4. Why the agent forgets you

Try this:

```
You: Senior .NET developer, 6 years
You: Make the questions harder
```

The second answer has no idea what "the questions" were. **LLMs are stateless**: each request only contains what you send. Because `RunStreamingAsync(userPrompt)` is called without a session, every turn starts from scratch.

The video's **agent sessions** section shows exactly this: after the three questions, it asks "show me the last questions asked", and without a session the agent has no idea.

To give the agent memory, create a session once and pass it on every turn (Exercise 1). The session keeps the conversation history and resends it with each request. That's how every chatbot "remembers", and why long chats get slower and more expensive: the prompt grows each turn.

---

## 5. What instructions can and cannot do

The instruction says *"its timebound for 1 minute"*. The model cannot enforce that. It has no clock, and it can't stop the user from taking longer. At most it will mention the time limit in its text.

That's a useful rule for every AI app:

| Instructions CAN | Instructions CANNOT |
|---|---|
| Shape the output (format, tone, number of items) | Enforce time limits, permissions or budgets |
| Set a role ("you are an interviewer") | Guarantee the model obeys every time |
| Give context the model should use | Access data you didn't send |

Anything that must *always* be true belongs in code. Chapter 1 (approval gates) and Chapter 7 (guardrails) are built on this idea.

---

## 6. How this leads to options 2–4

Option 1 has a weakness: the questions come only from the model's general knowledge. Ask twice and you get different questions; ask about a niche role and it may guess.

The next options fix that by **grounding** the model in your own data:

| Option | Adds |
|---|---|
| [2 — Seed](02-SeedExperiences.md) | Stores curated profiles and their topics as embeddings in SQL Server |
| [3 — Interview](03-InterviewFlow.md) | Finds the closest stored profile with vector search, then asks the agent to write questions from it |
| [4 — MCP server](../Mcp/04-McpServer.md) | Lets other AI apps (Cursor, Claude Desktop) query the same data |

This "retrieve, then generate" pattern is the core of RAG, which Chapter 3 covers in depth.

---

## 7. Practice exercises

1. **Add memory.** Create a session before the loop and pass it on every turn, then try the "make them harder" test from §4 again.

   ```csharp
   var session = await agent.CreateSessionAsync();
   // inside the loop:
   await foreach (var update in agent.RunStreamingAsync(userPrompt, session))
   ```

2. **Tighten the instructions.** Rewrite them so the output is always exactly three numbered questions with no intro text. Run the same input five times and check how consistent it is.
3. **Switch models.** Set `OpenAI:ChatModel` to `gpt-4o-mini` in `appsettings.Local.json`. Compare quality and speed. No code change is needed.
4. **Non-streaming.** Replace `RunStreamingAsync` with `RunAsync` and print `response.Text`. Notice the pause before anything appears.

---

## 8. Following along with the video

The [video](https://www.youtube.com/watch?v=lEUPgdv0gY8) and this repo teach the same ideas, but a few details differ. If something on screen doesn't match the code here, check this table:

| In the video | In this repo |
|---|---|
| The API key is read from an environment variable | `appsettings.Local.json`, or the `OPENAI_API_KEY` environment variable (both work) |
| The model name (GPT-4) is written in code | `OpenAI:ChatModel` in `appsettings.json` |
| The OpenAI NuGet package is pinned to **2.12**, because of async-stream issues in later versions at recording time | Built against **OpenAI 2.14** with **Microsoft.Agents.AI 1.21** (see `AIMLAPP.csproj`). Match these versions if you copy code from here. |
| `agent.RunAsync(...)` prints the whole answer at once | `RunStreamingAsync(...)` prints tokens as they arrive (§3.3) |
| Agent sessions are demonstrated in their own lab | Left as Exercise 1, so you can see the stateless behaviour first |
| Tool calling (a `SendEmail` function registered with `AIFunctionFactory.Create`) is shown next | Covered in depth in [Chapter 1](../Learning/01-FunctionCalling.md) (option 5) and [Chapter 2](../Learning/02-SemanticKernel.md) (option 6) |

Next in the video: vectors, embeddings and cosine similarity. That's [option 2](02-SeedExperiences.md).
