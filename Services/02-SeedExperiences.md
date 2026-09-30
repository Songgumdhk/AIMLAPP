# Option 2 — Seed the Experiences Table

> **Goal:** Turn text into **embeddings** (vectors of numbers that capture meaning) and store them in SQL Server's native `VECTOR` column, so option 3 can search by meaning instead of by keywords.

Code: [`Services/ExperienceSeeder.cs`](ExperienceSeeder.cs) · entity [`Models/Experience.cs`](../Models/Experience.cs) · context [`Data/AppDbContext.cs`](../Data/AppDbContext.cs)

> **Watch first:** this option matches the **vectors and embeddings** part of
> [C# AI ML Tutorial for Beginners | Agents, RAG and MCP](https://www.youtube.com/watch?v=lEUPgdv0gY8)
> (Questpond). That part covers why RAG needs semantic search, what a vector is, cosine vs Euclidean,
> `text-embedding-3-small`, SQL Server 2025's vector column, and the background job that turns every
> `Experience` row into a vector. Differences from the video are in [§9](#9-following-along-with-the-video).

---

## Table of Contents

1. [Before you run it](#1-before-you-run-it)
2. [What you'll see](#2-what-youll-see)
3. [What an embedding is](#3-what-an-embedding-is)
4. [How the seeder works](#4-how-the-seeder-works)
5. [What gets embedded, and why](#5-what-gets-embedded-and-why)
6. [Storing vectors in SQL Server](#6-storing-vectors-in-sql-server)
7. [Things to watch out for](#7-things-to-watch-out-for)
8. [Practice exercises](#8-practice-exercises)
9. [Following along with the video](#9-following-along-with-the-video)

> **Settings for this option** live in `appsettings.json`: `OpenAI:EmbeddingModel` and
> `OpenAI:EmbeddingDimensions` (they must match each other and the SQL column).
> Your API key and `ConnectionStrings:AimlDatabase` go in `appsettings.Local.json`.

---

## 1. Before you run it

You need SQL Server 2025 or Azure SQL Database, anything with the `VECTOR` type. Create the table once:

```sql
CREATE TABLE Experiences (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    Experience  NVARCHAR(MAX) NOT NULL,
    ExpVector   VECTOR(1536)  NULL,
    Questions   NVARCHAR(MAX) NULL
);
```

`1536` is the output size of `text-embedding-3-small`, the default `OpenAI:EmbeddingModel`.

---

## 2. What you'll see

```
Selection: 2
Seeding Experiences table...
  Embedding: "Junior .NET developer with 0-2 years of experience"
  Embedding: "Senior AI engineer with 5+ years of experience"
  ...
Seed complete. 17 row(s) inserted.
```

The "Embedding" lines appear in a different order each run, because all 17 requests run at the same time (§4).

---

## 3. What an embedding is

An embedding model turns text into a fixed-length list of numbers:

```
"Senior .NET developer with 5-8 years"  →  [0.012, -0.044, 0.031, ... 1536 numbers]
```

Texts with similar **meaning** end up with vectors that point in similar directions, even when they share no words:

| Text A | Text B | Similar? |
|---|---|---|
| "Senior .NET developer, 6 years" | "Senior .NET developer with 5-8 years of experience" | Very |
| "Backend C# engineer, mid-level" | "Mid-level .NET developer with 3-5 years" | Yes, with no shared keywords |
| "Senior .NET developer" | "Junior frontend developer" | Much less |

That's what makes "search by meaning" possible. A keyword search for "C# engineer" would never find ".NET developer".

**Cosine vs Euclidean.** The video compares both. Euclidean distance measures how *far apart* two points are, which suits things like map coordinates. Cosine measures whether two vectors point the *same way*, which is what "similar meaning" is. That's why this app uses cosine throughout.

---

## 4. How the seeder works

```
17 sample profiles (in code)
        │  one embedding request per profile, all in parallel
        ▼
OpenAI embeddings API  ──►  17 vectors
        │
        ▼
EF Core AddRangeAsync + SaveChangesAsync  ──►  17 rows in Experiences
```

The key lines:

```csharp
var embeddingClient = new EmbeddingClient(EmbeddingModel, openAiApiKey);

var experiences = await Task.WhenAll(
    SampleExperiences.Select(kvp => BuildExperienceAsync(kvp.Key, kvp.Value, embeddingClient)));
```

`Task.WhenAll` starts all 17 requests at once and waits for them together, which is much faster than one after another. Each call builds one row:

```csharp
var embedding = await embeddingClient.GenerateEmbeddingAsync(experienceText);

return new Experience
{
    ExperienceText = experienceText,
    ExpVector = new SqlVector<float>(embedding.Value.ToFloats()),
    Questions = questions,
};
```

Then a single `SaveChangesAsync` writes all rows together in one transaction; EF Core batches the inserts for you.

---

## 5. What gets embedded, and why

Each sample has two parts:

```csharp
["Senior .NET developer with 5-8 years of experience"] =      // ← embedded
    "ASP.NET Core, EF Core performance tuning, design patterns, ..."  // ← stored as text
```

Only the **profile description** is embedded. The topics in `Questions` are stored as plain text.

**Why:** in option 3 the user types a description of *themselves* ("senior .NET dev, 6 years"). Search works best when you compare like with like, so the stored vector should represent the same kind of text the user will type. The topics are the *payload* you return once a match is found, not the thing you search on.

This is the same split every RAG system makes: **what you search on** versus **what you hand to the model**.

---

## 6. Storing vectors in SQL Server

The entity uses `SqlVector<float>` from `Microsoft.Data.SqlTypes`:

```csharp
[Column("ExpVector")]
public SqlVector<float>? ExpVector { get; set; }
```

And `AppDbContext` tells EF Core the exact column type, read from config:

```csharp
modelBuilder.Entity<Experience>()
    .Property(e => e.ExpVector)
    .HasColumnType($"vector({VectorDimensions})");
```

Keeping vectors in the same database as the rest of your data means no separate vector database to run, back up and secure. For thousands to low millions of rows, that's often the simplest good choice. The video makes the other side of the argument: dedicated vector databases such as Qdrant, Pinecone, or Postgres with pgvector offer more vector features at scale. Many real systems keep relational data in SQL and vectors in a vector store.

> **The video's quiz: why does a 1536-dimension vector take 6152 bytes?**
> Each dimension is a 4-byte `float`, so 1536 × 4 = 6144 bytes, plus an 8-byte header = 6152.

---

## 7. Things to watch out for

- **Running it twice duplicates the data.** The seeder always inserts; it never checks what's already there. To start over, run `TRUNCATE TABLE Experiences;` and seed again.
- **The model and the column must agree.** If you change `OpenAI:EmbeddingModel`, set `OpenAI:EmbeddingDimensions` to its output size, recreate the table with the new `VECTOR(N)`, and re-seed. Vectors from different embedding models can't be compared, even when they happen to be the same length.

  This answers the video's discussion question, "which hurts more: changing the LLM or changing the embedding model?" Changing the **chat model** is one setting (`OpenAI:ChatModel`) and your data stays as it is. Changing the **embedding model** means re-embedding every stored row, and possibly resizing the column, before search works again. The embedding model is the harder one to change.
- **Cost is tiny.** 17 short texts are a few hundred tokens. At `text-embedding-3-small` prices, that's a fraction of a cent.

---

## 8. Practice exercises

1. **Add your own profile.** Add a new entry to `SampleExperiences` (for example "Game developer with Unity, 3 years"), truncate the table, re-seed, then find it with option 3.
2. **Batch the requests.** Replace the 17 parallel calls with one call to `embeddingClient.GenerateEmbeddingsAsync(listOfTexts)`. One request instead of 17 is gentler on rate limits.
3. **Make it idempotent.** Skip profiles whose `ExperienceText` already exists, so running option 2 twice inserts nothing new.
4. **Look at a vector.** Print the first 5 numbers and the length of one embedding. Check that the length matches `OpenAI:EmbeddingDimensions`.

---

## 9. Following along with the video

| In the video | In this repo |
|---|---|
| The `Experiences` rows already exist. A background step reads them with ADO.NET (`SqlDataReader`) and `UPDATE`s each row's vector | The seeder defines the profiles in code, embeds them, and `INSERT`s complete rows with EF Core |
| The vector is sent as a string parameter and cast to `VECTOR` in SQL | `SqlVector<float>` is mapped straight to the `vector(1536)` column by EF Core |
| A separate demo compares two texts with cosine and Euclidean and prints the similarity (about 0.57 for "one year junior" vs "junior developer 0-2 years") | Not part of this option. To see cosine similarity computed in plain C#, run [Chapter 8](../Learning/08-LocalAI.md) (option 12), which needs no API key |
| Profiles such as "0 to 3 years .NET" with topics like OOP and SQL | 17 profiles across .NET, frontend, cloud, data/AI, mobile, QA and security |

Next in the video: putting the retrieved context in front of the agent (RAG). That's [option 3](03-InterviewFlow.md).
