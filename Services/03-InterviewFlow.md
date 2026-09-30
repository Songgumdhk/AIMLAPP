# Option 3 — Interview Flow (Vector Search + Agent)

> **Goal:** Build a complete, small RAG pipeline: embed the user's text, find the closest stored profile with SQL Server vector search, then have an agent write interview questions **grounded** in that profile.

Code: [`Services/InterviewFlow.cs`](InterviewFlow.cs)

> **Watch first:** this option matches the **agent + RAG** part of
> [C# AI ML Tutorial for Beginners | Agents, RAG and MCP](https://www.youtube.com/watch?v=lEUPgdv0gY8)
> (Questpond). That part covers why a plain agent gives generic questions, **Retrieve → Augment →
> Generate**, and the demo that embeds "one year junior C# developer", finds the closest SQL Server row by
> cosine, and adds its topics to the prompt. Differences from the video are in [§10](#10-following-along-with-the-video).

---

## Table of Contents

1. [Before you run it](#1-before-you-run-it)
2. [What you'll see](#2-what-youll-see)
3. [The pipeline](#3-the-pipeline)
4. [Step 1: embed the user's text](#4-step-1-embed-the-users-text)
5. [Step 2: vector search in SQL Server](#5-step-2-vector-search-in-sql-server)
6. [Step 3: a grounded agent](#6-step-3-a-grounded-agent)
7. [Option 1 vs option 3](#7-option-1-vs-option-3)
8. [Limitations to know](#8-limitations-to-know)
9. [Practice exercises](#9-practice-exercises)
10. [Following along with the video](#10-following-along-with-the-video)

> **Settings for this option** live in `appsettings.json`: `OpenAI:EmbeddingModel` (must be the
> same model used by option 2) and `OpenAI:ChatModel` (writes the questions).
> Your API key and `ConnectionStrings:AimlDatabase` go in `appsettings.Local.json`.

---

## 1. Before you run it

Create the table and run [option 2](02-SeedExperiences.md) first. With an empty table, every search reports "No matching experience with questions was found."

---

## 2. What you'll see

```
Selection: 3

Describe your experience / skill level (type 'exit' to quit):
> I build APIs in C# and have about 4 years of experience

Generating embedding...
Searching for closest matching experience...

Matched profile : Mid-level .NET developer with 3-5 years of experience
------------------------------------------------------------
Interview questions:

1. How do you structure an ASP.NET Core Web API so ...
2. ...
5. ...
============================================================
```

The input never said ".NET", yet it matched the .NET profile. That's search by meaning.

---

## 3. The pipeline

```
"I build APIs in C#, ~4 years"
        │
        ▼  (1) embedding model
[0.021, -0.013, ...]  user vector
        │
        ▼  (2) SQL Server: ORDER BY VECTOR_DISTANCE('cosine', ExpVector, @user)
Closest row: "Mid-level .NET developer..." + its topics
        │
        ▼  (3) chat model, with the row placed in the instructions
5 interview questions
```

Two OpenAI calls per turn (one embedding, one chat) and one SQL query.

---

## 4. Step 1: embed the user's text

```csharp
var embedding = await embeddingClient.GenerateEmbeddingAsync(userExperience);
var userVector = new SqlVector<float>(embedding.Value.ToFloats());
```

**Critical rule:** use the **same embedding model** that created the stored vectors. Each model has its own "coordinate system", so comparing vectors from two different models gives meaningless distances. Both option 2 and option 3 read `OpenAI:EmbeddingModel`, so they always agree.

---

## 5. Step 2: vector search in SQL Server

```csharp
var match = await db.Experiences
    .AsNoTracking()
    .OrderBy(e => EF.Functions.VectorDistance("cosine", e.ExpVector!.Value, userVector))
    .FirstOrDefaultAsync();
```

EF Core translates this to SQL roughly like:

```sql
SELECT TOP(1) Id, Experience, ExpVector, Questions
FROM Experiences
ORDER BY VECTOR_DISTANCE('cosine', ExpVector, @userVector);
```

**Cosine distance** measures the angle between two vectors:

| Distance | Meaning |
|---|---|
| 0 | Same direction: same meaning |
| Lower | More related |
| Around 1 | Unrelated |
| 2 | Opposite (rare with text) |

The exact range for "related" depends on the embedding model, so measure real inputs before choosing a cut-off (Exercise 1). Smaller is closer, so `OrderBy` + `FirstOrDefault` returns the best match. `AsNoTracking()` tells EF Core not to track the row for changes. It's read-only here, so that saves work.

The search runs **inside the database**. Only one row comes back over the network, not all the vectors.

---

## 6. Step 3: a grounded agent

The matched row is placed straight into the agent's instructions:

```csharp
var instructions = $"""
    You are a technical interviewer. Based on the matched candidate profile
    and the interview questions listed below, generate exactly 5 technical
    interview questions ...

    Matched candidate profile: {match.ExperienceText}

    Source questions:
    {match.Questions}
    """;

AIAgent agent = chatClient.AsAIAgent(name: "Interviewer", instructions: instructions);
```

This is the "augmented generation" in **RAG** (Retrieval-Augmented Generation). The video spells out the three words: **Retrieve** the closest row, **Augment** the prompt by adding its topics, **Generate** the answer. The model isn't asked to invent questions from general knowledge; it's given *your* curated topics and asked to turn them into questions. The prompt also locks down the output: exactly 5 questions, numbered, no greetings or commentary.

A new agent is created for every match because the instructions change each time. Agents are cheap objects; the expensive part is the model call.

---

## 7. Option 1 vs option 3

| | Option 1 (plain agent) | Option 3 (grounded agent) |
|---|---|---|
| Where the questions come from | The model's general knowledge | Your curated topics in the database |
| Consistency | Varies run to run | Anchored to the same topics |
| Updating content | Edit the prompt | Edit rows in the table, no redeploy |
| Cost per turn | 1 chat call | 1 embedding + 1 SQL query + 1 chat call |

---

## 8. Limitations to know

- **There's always a match.** Nearest-neighbour search returns the closest row even when nothing is close. Type "professional chef, 10 years" and you'll still get a tech profile. Real systems add a **distance threshold** (Exercise 2).
- **Wording matters.** In the video, "12 plus years of experience" first matched the *junior* profile, and only a rephrased input picked the architect profile. Short, number-heavy inputs carry little meaning for an embedding model. Printing the distance (Exercise 1) makes these weak matches easy to spot.
- **Top 1 only.** Someone who is half .NET and half DevOps matches only one profile. Taking the top 2–3 and merging their topics gives richer questions (Exercise 3).
- **Brute-force search.** `ORDER BY VECTOR_DISTANCE` compares against every row. That's fine for thousands of rows. For millions, you'd add a vector index for approximate search.
- **One DbContext for the whole session.** The loop reuses one `AppDbContext`, which is fine for a console demo. In a web app, use one context per request.

---

## 9. Practice exercises

1. **Show the distance.** Select the distance along with the row (`Mcp/ExperienceMcpTools.cs` already does this) and print it next to "Matched profile". Try good, vague and off-topic inputs to build intuition for the numbers.
2. **Reject weak matches.** Print "No close profile" when the distance is above a cut-off you picked from Exercise 1. Try the chef example again.
3. **Top-3 retrieval.** Use `.Take(3).ToListAsync()` and pass all three profiles to the agent. Do the questions get better for mixed backgrounds?
4. **Compare to option 1.** Give options 1 and 3 the same description and compare the questions side by side.

---

## 10. Following along with the video

| In the video | In this repo |
|---|---|
| The search is a hand-written ADO.NET query: `SELECT TOP 1 ... ORDER BY VECTOR_DISTANCE('cosine', ...)` | `EF.Functions.VectorDistance(...)` in LINQ, which EF Core translates to the same SQL (§5) |
| The retrieved topics are **added to the user prompt** ("... ask questions around OOP and SQL") | The matched profile and topics are placed in the agent's **instructions** (system prompt). Both are valid ways to augment. Instructions keep the user's own text untouched. |
| Three questions, like option 1 | Exactly five questions, in a strict numbered format |
| Runs once per start | Loops until you type `exit` |

After RAG, the video moves on to **agent sessions** (see the option 1 guide, [§4](01-ChatWithAgent.md#4-why-the-agent-forgets-you)), then **tool calling** ([Chapter 1](../Learning/01-FunctionCalling.md)), and finally **MCP**. That's [option 4](../Mcp/04-McpServer.md).
