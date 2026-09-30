# Chapter 3 — Advanced RAG

> **Goal:** Move beyond "toy RAG" (embed → search → stuff into prompt) and
> learn the five techniques that make RAG actually work in production:
> **Chunking, Hybrid Search, Reranking, Metadata Filtering, Query Rewriting.**
>
> If you only go deep on one chapter, make it this one.

---

## Table of Contents

1. [What is RAG (recap)?](#1-what-is-rag-recap)
2. [Why "basic RAG" fails in production](#2-why-basic-rag-fails-in-production)
3. [The Advanced RAG pipeline](#3-the-advanced-rag-pipeline)
4. [Chunking Strategies](#4-chunking-strategies)
5. [Vector Search — a mini refresher](#5-vector-search--a-mini-refresher)
6. [Keyword Search (BM25)](#6-keyword-search-bm25)
7. [Hybrid Search + Reciprocal Rank Fusion](#7-hybrid-search--reciprocal-rank-fusion)
8. [Reranking](#8-reranking)
9. [Metadata Filtering](#9-metadata-filtering)
10. [Query Rewriting](#10-query-rewriting)
11. [Putting it all together](#11-putting-it-all-together)
12. [Common Pitfalls](#12-common-pitfalls)
13. [Practice Exercises](#13-practice-exercises)

> **Settings for this chapter** live in `appsettings.json`. Every number the
> demo uses below comes from there, so you can experiment without touching code:
>
> | Setting | Default | Used for |
> | --- | --- | --- |
> | `OpenAI:ChatModel` | `gpt-4o` | Query rewriting, reranking, answering |
> | `OpenAI:EmbeddingModel` | `text-embedding-3-small` | Chunk and query vectors |
> | `Rag:TopK` | 5 | Chunks kept for the answer |
> | `Rag:RerankCandidates` | 20 | Candidate pool handed to the reranker |
> | `Rag:RrfK` | 60 | RRF smoothing constant |
> | `Rag:QueryRewriteCount` | 3 | Variants produced by query rewriting |
> | `Rag:FixedChunkSize` / `Rag:FixedChunkOverlap` | 300 / 50 | Fixed-size chunking (characters) |
> | `Rag:SentencesPerChunk` | 3 | Sentence chunking |

---

## 1. What is RAG (recap)?

**Retrieval-Augmented Generation** = give the LLM your private data at
question-time by:

1. **Retrieving** a small handful of relevant text snippets from a corpus
2. **Augmenting** the LLM prompt with those snippets as context
3. **Generating** an answer grounded in that context

```
User question ──▶ Retrieve top-K docs ──▶ Stuff into prompt ──▶ LLM ──▶ Answer
```

The LLM's weights don't change. You're just handing it a cheat sheet at
inference time. That's why RAG is cheaper and safer than fine-tuning.

---

## 2. Why "basic RAG" fails in production

Basic RAG is:
1. Split doc into fixed 500-char chunks
2. Embed each chunk with OpenAI
3. Store vectors in Pinecone/Qdrant/pgvector
4. At query time: embed the query, cosine-similarity search, top-5 chunks
5. Prompt = "Given: [chunks]\n\nAnswer: [question]"

This works for a demo. In production it hits five walls:

| Wall                         | What breaks                                                                   |
| ---------------------------- | ----------------------------------------------------------------------------- |
| **Bad chunking**             | Chunk cuts mid-sentence, splits table headers from rows, loses context.       |
| **Semantic vs lexical gap**  | User searches for "SKU 42B" — a *number*. Embeddings are bad at exact IDs.    |
| **Wrong ordering**           | Top-5 by cosine similarity ≠ top-5 by actual relevance to the *question*.     |
| **No filtering**             | User asks about "HR policies" but gets Engineering docs mixed in.             |
| **Vague queries**            | *"How do I fix it?"* has no keywords. Nothing matches.                        |

**Advanced RAG** = five techniques that address each wall.

---

## 3. The Advanced RAG pipeline

```
┌─────────────────────────────────────────────────────────────────┐
│                     INGESTION (offline)                         │
│                                                                 │
│   Raw doc ──▶ Chunk ──▶ Embed ──▶ Store (vector + keyword)     │
│                                    with METADATA               │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                     QUERY TIME (online)                         │
│                                                                 │
│   User query                                                    │
│      │                                                          │
│      ├──▶ (5) Rewrite / expand into multiple queries           │
│      │                                                          │
│      ├──▶ (9) Filter candidates by METADATA                    │
│      │                                                          │
│      ├──▶ (7) HYBRID retrieval:                                 │
│      │        (5) Vector search  ──┐                            │
│      │                             ├─▶ RRF ──▶ top 20          │
│      │        (6) Keyword search ──┘                            │
│      │                                                          │
│      ├──▶ (8) RERANK the 20 to get top 5                       │
│      │                                                          │
│      └──▶ Stuff top 5 into LLM prompt ──▶ Answer               │
└─────────────────────────────────────────────────────────────────┘
```

The numbers in parentheses refer to sections in this doc. In the demo,
"top 20" is `Rag:RerankCandidates` and "top 5" is `Rag:TopK`. Every
technique adds latency but improves relevance. Real systems pick which
stages they need.

---

## 4. Chunking Strategies

Chunking is the single most under-rated RAG lever. Bad chunks = bad
retrieval, no matter how good your vector DB is.

### 4.1 Fixed-size chunking

Split every N characters, with M-character overlap so context isn't lost
across boundaries.

```
[0..500] "The quick brown fox jumps over the..."
[450..950] "...over the lazy dog. Lorem ipsum dolor..."
[900..1400] "...ipsum dolor sit amet. Consectetur..."
```

**Pros:** dead simple, deterministic.
**Cons:** cuts mid-sentence/word. Terrible for structured content (tables,
code). Overlap wastes storage and dilutes results.

The demo's fixed-size option uses N = `Rag:FixedChunkSize` and
M = `Rag:FixedChunkOverlap` (300 and 50 characters by default).

### 4.2 Sentence-based chunking

Split on sentence boundaries. Group N sentences per chunk.

**Pros:** never cuts mid-sentence.
**Cons:** short sentences → tiny chunks with no context; long sentences
(legal, technical) → huge chunks.

### 4.3 Paragraph / heading-based chunking

Split on `\n\n` or on Markdown/HTML headings.

**Pros:** respects the author's structure — usually best for docs written
by humans.
**Cons:** requires the source to have structure. PDFs often don't.

### 4.4 Semantic chunking

Embed successive sentences, split where cosine similarity between adjacent
sentences drops below a threshold. Effectively: split where the topic changes.

**Pros:** highest-quality chunks.
**Cons:** expensive (extra embeddings per doc), needs tuning.

### 4.5 Parent-child (hierarchical) chunking

Store **small child chunks** for retrieval, but at inference-time return
the **larger parent chunk** (or the whole doc) as context. Best of both:
precise retrieval + rich context.

**Rule of thumb:**

| Content type      | Best strategy                    |
| ----------------- | -------------------------------- |
| Prose docs, wikis | Paragraph or semantic            |
| PDFs (unstructured) | Fixed-size with overlap        |
| Code              | Function/class boundaries        |
| Chat logs         | Per turn or per session          |
| Structured data   | Row / record boundaries          |

---

## 5. Vector Search — a mini refresher

**Vector search** = find the docs whose embedding is nearest to the query
embedding, using **cosine similarity**:

```
cosine(a, b) = (a · b) / (||a|| × ||b||)
             = a normalized dot product, range [-1, 1]
```

For OpenAI embeddings (already normalized), cosine similarity = dot product,
range roughly [0, 1] for related content.

**Strengths:** captures *meaning*. "car" matches "automobile."
**Weaknesses:** bad at *exact tokens*. "MSFT" won't match "Microsoft" as
well as you'd hope. Terrible at IDs, SKUs, error codes, function names.

That's why you need...

---

## 6. Keyword Search (BM25)

**BM25** is the classic full-text scoring algorithm — same tech as
Elasticsearch / Postgres full-text / Lucene.

Given a query with terms $t_1, t_2, ..., t_n$ and a document $D$:

$$\text{BM25}(D, Q) = \sum_i \text{IDF}(t_i) \cdot \frac{f(t_i, D) \cdot (k_1 + 1)}{f(t_i, D) + k_1 \cdot (1 - b + b \cdot \frac{|D|}{\text{avgdl}})}$$

Don't memorize the formula. The intuition:

- **Term frequency** (TF): more occurrences of the term in the doc → higher score
- **Inverse document frequency** (IDF): rare terms score higher than common ones
- **Length normalization**: shorter docs get a slight boost

**Strengths:** perfect for exact matches ("SKU 42B", error codes, names).
**Weaknesses:** zero semantic understanding. "car" and "automobile" score
nothing in common.

Now the magic — combine both.

---

## 7. Hybrid Search + Reciprocal Rank Fusion

**Hybrid search** = run vector search AND keyword search, then merge.

The naive merge is: take top-5 from each, deduplicate, done. Better:
**Reciprocal Rank Fusion (RRF)**.

### RRF formula

For each document $d$, if it appeared at rank $r_v$ in the vector results
and rank $r_k$ in the keyword results:

$$\text{RRF}(d) = \frac{1}{k + r_v} + \frac{1}{k + r_k}$$

(where `k ≈ 60` is a smoothing constant. The demo reads it from `Rag:RrfK`.)

Docs that rank high in **both** lists get the highest score. Docs that
appear in only one list still score, just lower.

### Why RRF > "just average the scores"

- Vector scores are in `[0, 1]`
- BM25 scores are unbounded (can go up to 20+)
- Averaging apples and oranges = garbage

RRF uses only the **rank**, not the raw score. Fair combination.

### Example

| Doc  | Vector rank | Keyword rank | RRF (k=60)                   |
| ---- | ----------- | ------------ | ---------------------------- |
| A    | 1           | 3            | 1/61 + 1/63 = **0.0323**     |
| B    | 2           | 1            | 1/62 + 1/61 = **0.0325** ← winner |
| C    | 3           | (not found)  | 1/63 = 0.0159                |
| D    | (not found) | 2            | 1/62 = 0.0161                |

B wins because it ranks high in both. C and D each rank high in only one.

---

## 8. Reranking

After hybrid search you have maybe 20 candidates. Now use a **reranker**
(a more expensive model) to reorder them by *actual* relevance to the
query, and keep only the top 3–5.

Two flavors:

### 8.1 Cross-encoder rerankers (the "real" way)

Small transformer models (e.g. `bge-reranker-large`, `Cohere Rerank`)
that take `[query, doc]` as input and output a score. They're much more
accurate than bi-encoders (which is what embeddings are) because they
see both texts together.

Cost: ~10-100× cheaper than an LLM call, but not free.

### 8.2 LLM-as-reranker (poor man's version)

Send the query + candidates to your chat model (`OpenAI:ChatModel`,
`gpt-4o` by default) with a prompt like:

```
Rank these documents by relevance to the query.
Query: "..."
Docs:
1. ...
2. ...
Return JSON: [{"id": 3, "score": 0.9}, ...]
```

**Pros:** no extra dependency, "just works" with your existing LLM.
**Cons:** slow (1-2s per query), expensive at scale.

For the demo, we'll use LLM-as-reranker because it needs no extra service.
In production, use Cohere Rerank or a self-hosted cross-encoder.

### Why reranking helps so much

Embeddings compress 1000+ words of nuance into 1536 numbers. Cosine
similarity says "these vectors are close." A reranker says "given the
exact query, this doc is actually about it, and this one just happens to
share vocabulary."

**Typical impact:** +10-30% retrieval accuracy on real workloads.

---

## 9. Metadata Filtering

Every chunk should carry metadata:

```json
{
    "id": "doc42-chunk3",
    "content": "...",
    "embedding": [0.12, -0.44, ...],
    "metadata": {
        "source": "employee_handbook.pdf",
        "department": "HR",
        "category": "policy",
        "created": "2026-01-15",
        "sensitivity": "public"
    }
}
```

Two ways to use it:

### 9.1 Pre-filter (best when possible)

Before the vector/keyword search, restrict the candidate pool:

```csharp
var candidates = corpus.Where(d => d.Department == "HR" && d.Created > lastYear);
var results = HybridSearch(query, candidates);
```

**Pros:** fewer candidates → faster search + better precision.
**Cons:** may filter out relevant docs if the filter is too aggressive.

### 9.2 Post-filter

Run the search over everything, then drop hits that don't match:

```csharp
var results = HybridSearch(query, corpus).Where(d => d.Department == "HR");
```

**Pros:** doesn't miss anything.
**Cons:** wastes compute; you may end up with < K results after filtering.

### Real-world examples

- **Multi-tenant SaaS:** every chunk has `tenantId`; pre-filter is a hard security requirement.
- **Time-sensitive queries:** filter to docs updated in last 6 months.
- **Compliance:** exclude `sensitivity: "internal"` from public-facing bots.
- **Personalization:** filter to user's language, region, subscription tier.

**Rule of thumb:** always pre-filter on things that MUST match (tenancy,
permissions). Post-filter on things that SHOULD match (recency).

---

## 10. Query Rewriting

Users type garbage. The LLM writes clean queries.

### 10.1 Multi-query expansion

Take the user's query and ask the LLM: *"Rewrite this into 3 different
search queries that mean the same thing."*

Then embed and search with **all N queries**. Merge results with RRF.
The demo sets N with `Rag:QueryRewriteCount`.

**Example:**
- User: "How do I fix it"
- Rewrites:
  1. "How to troubleshoot the login error"
  2. "Steps to resolve authentication failures"
  3. "Password reset procedure"

Any doc that matches any rewrite gets a chance.

### 10.2 HyDE (Hypothetical Document Embeddings)

Ask the LLM to *invent* an ideal answer to the query. Then embed **that
hypothetical answer** and search with it.

**Why it works:** the hypothetical answer is written in the same language
as real docs, so it embeds closer to them than the raw question does.

```
Query:        "reset password"
Hypothetical: "To reset your password, navigate to the Account Settings
               page, click 'Forgot Password', enter your registered email..."
Embed the hypothetical, then search.
```

Sounds crazy. Works surprisingly well — often +10-15% accuracy.

### 10.3 Query decomposition

For complex questions: split into sub-questions, retrieve for each,
answer each, then combine.

*"Which candidate has more Python experience: Alice or Bob?"*
→ `search("Alice Python experience")`, `search("Bob Python experience")`,
combine.

---

## 11. Putting it all together

The full production RAG pipeline:

```
User query
    │
    ├─▶ (10) Rewrite into 3 variants + 1 HyDE
    │        │
    │        ▼
    │   For each variant:
    │        ├─▶ Embed
    │        ├─▶ (9) Pre-filter by metadata
    │        ├─▶ (5) Vector search top 30
    │        └─▶ (6) Keyword search top 30
    │        │
    │        ▼
    │   (7) RRF-merge → top 20
    │
    ├─▶ (8) Rerank 20 with cross-encoder → top 5
    │
    └─▶ Prompt: "Given [top 5], answer [original query]"
                     │
                     ▼
                  Answer
```

Not every app needs every stage. Start basic, add stages until quality
plateaus. Measure with an eval set (that's Chapter 5).

---

## 12. Common Pitfalls

| Pitfall                                                        | Fix                                                                  |
| -------------------------------------------------------------- | -------------------------------------------------------------------- |
| Chunks so big the LLM can't find the answer inside them        | Rechunk smaller. Aim for 200-500 tokens per chunk.                  |
| Chunks so small they lack context                              | Use parent-child chunking.                                           |
| Embedding the raw HTML/PDF with formatting                     | Clean first. Strip tags, normalize whitespace.                       |
| Same embedding model for query and docs but they're different  | ALWAYS use the same model for both sides.                            |
| Storing 3072-dim vectors when 512 would work                   | Try smaller embedding models (`text-embedding-3-small` at 512 dims). |
| Vector-only search on queries with IDs / codes                 | Add BM25 / hybrid.                                                   |
| Reranking 500 candidates                                       | Reranker cost is linear. Retrieve 20, rerank 20 — not 500.           |
| Query rewriting on every call, even for exact-match queries    | Skip rewrite if the query has enough info; use it for vague queries. |
| Not showing citations                                          | Always return chunk IDs so users can verify.                         |
| No metadata on chunks                                          | Retrofit is painful. Design metadata upfront.                        |

---

## 13. Practice Exercises

All exercises run from the demo (Program menu option **7**).

### Exercise 1 — See the chunking difference

At the demo start prompt for "chunking strategy":
- Pick **1 (fixed-size)** and search *"vacation policy"*
- Pick **3 (paragraph)** and search the same
- Compare which chunks were retrieved and how coherent they read.

Then change `Rag:FixedChunkSize` to 150 or 600 in `appsettings.json`,
rerun option 1, and see how the chunk boundaries move.

### Exercise 2 — Vector vs keyword vs hybrid

Ask *"What is SKU-2287?"* three times, once with each search mode:
- **Vector only** → likely misses (embeddings are bad at IDs)
- **Keyword only** → finds it
- **Hybrid** → finds it AND surrounding context

### Exercise 3 — See reranking change the order

Run any hybrid search. The demo prints top-K **before** rerank and
top-K **after** rerank side-by-side. Notice how the reranker often
promotes a doc that was rank-3-or-4 up to rank 1. K is `Rag:TopK`.

### Exercise 4 — Metadata pre-filter

Ask *"What are our security policies?"* with department filter set to
**Engineering** vs **HR** vs **(none)**. Confirm the retrieved chunks
respect the filter.

### Exercise 5 — Query rewriting saves the day

Ask a very vague question like *"How do I fix it?"* with:
- Rewriting **off** → few or bad hits
- Rewriting **on** → LLM expands into 3 realistic queries, hybrid RRF
  finds real answers

---

## What's Next?

Once every exercise here feels obvious, you'll be doing production-quality
RAG. Next chapter: **Agentic Workflows** (`04-AgenticWorkflows.md`, menu
option 8) — plan a task, carry state, remember facts, pause for a human,
and hand work between specialist agents.
