# Chapter 6 — Observability

> **Goal:** See what one AI request actually did. A trace names the steps.
> Token counts say how big each model call was. Latency says which step
> was slow. Cost turns those counts into money.
>
> The aim is to see token usage, the prompt, the response, latency, tool
> calls, errors, retrieval results, cost, and the agent steps.

---

## Table of Contents

1. [What you already have](#1-what-you-already-have)
2. [A trace is a tree](#2-a-trace-is-a-tree)
3. [Token usage](#3-token-usage)
4. [Latency](#4-latency)
5. [Cost](#5-cost)
6. [One request, all four views](#6-one-request-all-four-views)
7. [What a hosted tracer adds](#7-what-a-hosted-tracer-adds)
8. [Common Pitfalls](#8-common-pitfalls)
9. [Practice Exercises](#9-practice-exercises)

> **Settings for this chapter** live in `appsettings.json`:
>
> | Setting | Default | Used for |
> | --- | --- | --- |
> | `OpenAI:SmallChatModel` | `gpt-4o-mini` | The chat calls this chapter traces |
> | `OpenAI:ChatModel` | `gpt-4o` | The "what if" repricing (never called here) |
> | `OpenAI:EmbeddingModel` | `text-embedding-3-small` | Handbook and question vectors |
> | `OpenAI:Temperature` | 0 | Every chat call |
> | `Observability:AnswerMaxOutputTokens` | 120 | Output cap on `chat.answer` |
> | `Observability:CompareMaxOutputTokens` | 40 | Output cap on `chat.short` / `chat.long` |
> | `Pricing` | see §5 | Rate per 1M tokens for each model name |

---

## 1. What you already have

Chapter 5 tells you whether the answer was any good.
It does not tell you which step ran, how long that step took, or what it cost.

A request in this app is not one model call:

```
Request
  ↓
Embed the question
  ↓
Retrieve handbook docs
  ↓
Tool: read the top policy
  ↓
Chat: write the answer
  ↓
Response
```

Menu option **10** runs that path and keeps a record of it.
The chat model is `OpenAI:SmallChatModel` (`gpt-4o-mini`), so you can
repeat the lesson cheaply. The cost view also prices the same token counts
as `OpenAI:ChatModel` (`gpt-4o`). That second number is arithmetic. It does
not call `gpt-4o`.

---

## 2. A trace is a tree

A **trace** is one piece of work, grouped by a trace id.
A **span** is one step inside it. Spans nest.

```
trace a1b2c3d4
request                         ok
  embed.query                   ok
  retrieve                      ok
  tool.lookup_policy            ok
  chat.answer                   ok
```

`request` is the parent. The other four are children.
The parent does not call a model. It is the clock around the children.

Each span stores:

| Field | Why it is there |
| --- | --- |
| Name and kind | `embed`, `internal`, `tool`, `llm`, `server` |
| Start and duration | Measured with a stopwatch, not by subtracting wall-clock times |
| Status | `ok` or `error` |
| Detail | Retrieved ids, the tool's policy id, the model name |
| Prompt and output previews | This chapter's goal includes seeing the prompt and the response |
| Model and tokens | Only on the span that called the API |

Menu option **1** prints this tree for a handbook question.
Press Enter to ask how many vacation days a new hire gets.

The handbook is embedded once, on its own trace named `ingest.embed`.
The next question in the same session does not run that span again.
The first request pays for indexing. Later requests do not.

Under the real trace, option 1 prints a second, local tree. No model is called.
The question is policy `EXP-404`, which is not in the handbook.
The tool span is `error`. The request span stays `ok`.

That split is the point. A failed tool and a finished request are different
facts. You only see both when both statuses are stored. A catch block that
logs one line and moves on does not leave you a tree.

`SpanScope` is a `using` block. The span closes when the block ends,
including when the code inside throws. Duration still gets written.

---

## 3. Token usage

Menu option **2** reads the last request trace.
If you have not run one yet, it runs one.

The API reports three counts on a chat call:

| Count | Meaning |
| --- | --- |
| Input | Everything you sent: instructions, question, retrieved text, tool text |
| Output | What the model wrote back |
| Total | Input + output |

An embedding call has input tokens and no output tokens.
The vector is not billed as output text.

The table lists only spans whose `Model` is set.
`retrieve` and `tool.lookup_policy` are absent. They did not call a model.
`request` is absent too.

If a parent also stored the children's token totals, adding every row
would bill those calls twice. This tracer writes tokens only on the span
that made the call. `CostCalculator.ForTrace` sums those spans.

Cached input, when the API reports it, is already inside the input count.
The printer says so. It is not a second pile of tokens.
This demo still prices every input token at the full rate.
Cached tokens are cheaper in production. The number here is a ceiling.

`MaxOutputTokenCount` on the chat call is 120 (`Observability:AnswerMaxOutputTokens`).
A cap is part of observability: the output side cannot run away while you
are staring at a trace.

---

## 4. Latency

Menu option **3** prints start time, duration, and share of the root.

```
Span                     Start   Duration    Share
request                      0      800 ms    100%
  embed.query               10      120 ms     15%
  retrieve                 140        1 ms      0%
  tool.lookup_policy       142        0 ms      0%
  chat.answer              150      640 ms     80%
```

The numbers on your machine will differ. The shape is the lesson.
Search over four documents is noise next to the chat call.
A log line that only says "request took 800 ms" blames the whole request.
The trace tells you the chat span was the slow part.

Start offsets come from one `Stopwatch` on the tracer.
A stopwatch measures elapsed time. Subtracting `DateTime.Now` from
`DateTime.Now` can jump when the clock is corrected.

Children of a sequential pipeline should add up to a bit less than the
parent. The gaps are your own code between the spans. If you later run
two tool calls at the same time, their durations overlap, and the parent
is closer to the slower child than to the sum. Chapter 1 already runs
tool calls in parallel. The trace is how you would see that overlap.

---

## 5. Cost

Price is tokens times a rate. The rates live in the `Pricing` section of
`appsettings.json`, keyed by model name; `PriceTable` looks them up.
They are standard OpenAI prices per 1 million tokens for September 2026.
Edit `appsettings.json` when the price page changes. Do not bury a second
copy in a comment. If you switch to a model with no `Pricing` entry, the
cost view stops and tells you to add one.

```json
"Pricing": {
  "gpt-4o": { "InputPerMillion": 2.50, "OutputPerMillion": 10.00 },
  "gpt-4o-mini": { "InputPerMillion": 0.15, "OutputPerMillion": 0.60 },
  "text-embedding-3-small": { "InputPerMillion": 0.02, "OutputPerMillion": 0 }
}
```

| Model | Input, per 1M | Output, per 1M |
| --- | --- | --- |
| `gpt-4o` | $2.50 | $10.00 |
| `gpt-4o-mini` | $0.15 | $0.60 |
| `text-embedding-3-small` | $0.02 | $0 |

On these two chat models, one output token costs the same as four input tokens.
$10 / $2.50 = 4. The mini rates have the same ratio.

`CostCalculator.VerifyWorkedExample()` checks this case before the menu,
with no API call. It uses the rates printed above, not `appsettings.json`,
so it tests the formula and keeps passing when you edit prices:

```
1,000 input and 500 output on gpt-4o

input  = 1000 / 1,000,000 * 2.50 = 0.0025
output =  500 / 1,000,000 * 10.00 = 0.0050
total  = 0.0075
```

The same counts on `gpt-4o-mini` are $0.00045.
One thousand embedding tokens are $0.00002.

Menu option **4** prints that table first.
If a request trace is already in memory, it prices that trace twice:
once at mini rates, once with the chat spans repriced as `gpt-4o`.
Embeddings stay on the embedding rate in both totals.

Then it offers two real chat calls. The question is "what is 2+2?".
`chat.short` sends that question.
`chat.long` sends the same question plus a repeated handbook note.
Output length stays small because the instruction asks for one sentence
and the cap is 40 tokens (`Observability:CompareMaxOutputTokens`). Input
tokens jump. The extra money is the text
you stuffed into the prompt.

---

## 6. One request, all four views

Menu option **5** asks a question and prints the tree, the tokens,
the latency table, and the cost.

Read the cost block in this order:

| Line | What it is |
| --- | --- |
| Each model span | That call, at its own model price |
| THIS TRACE | Sum of those spans. The request parent adds $0 |
| IF CHAT WERE gpt-4o | Same measured tokens, chat repriced at `OpenAI:ChatModel` |
| SESSION SO FAR | Every API trace this process has run, including `ingest.embed` |

`THIS TRACE` and `SESSION SO FAR` differ after the first question
because indexing was a separate trace. That is the bill, not a bug.

Ask a second question from option 1 or 5.
The console says the handbook is already indexed.
The new tree has no `ingest.embed`. The session total still includes the first index.

---

## 7. What a hosted tracer adds

This demo keeps spans in a list and prints them.
The fields are the same ones a production tracer carries.

| This demo | OpenTelemetry / LangSmith |
| --- | --- |
| `TraceId` | Trace id, so one request can be found among thousands |
| Parent span id | The tree |
| Kind, status, error | What failed |
| Model, tokens, retrieved ids | Attributes on the span |
| Printed at the end | Exported while the request is running, to a collector you can search later |

LangSmith is a hosted place to look at those spans, aimed at LLM apps.
OpenTelemetry is the vendor-neutral way to emit them from .NET, next to
your existing HTTP and database spans. The decision in either tool is the
same decision this chapter makes in `SpanScope`: which steps are spans,
and which facts you attach.

Sampling, dashboards, and alerts sit on top of that. They are useless
until the span tree is the real pipeline and the token counts are the
counts the API returned.

---

## 8. Common Pitfalls

| Pitfall | What to do instead |
| --- | --- |
| One log line for the whole request | A span per step that can fail or cost money |
| Timing with wall-clock subtraction | A stopwatch for duration |
| Summing every span's tokens, including parents | Store tokens only on the span that called the model |
| Treating cached tokens as extra tokens | They are a subset of input. Price them at the cached rate when you have one |
| A cost number with no price table | `Pricing` in `appsettings.json` is the rate card. Date it. Change it in one place |
| Assuming the slow step is retrieval | Read the share column. On this handbook, chat dominates |
| Logging the API key, or the full prompt when it holds secrets | Store a preview, or redact. This demo's prompts are a public handbook |
| Dropping a tool error because the request still returned text | Status on the tool span stays `error` |
| Comparing today's bill with yesterday's after a price change | The trace keeps token counts. Reprice from the table. Do not store only dollars |

---

## 9. Practice Exercises

All of these run from menu option **10**.

### Exercise 1 — Read the tree

Option **1**. Press Enter for the vacation question.

Name the parent and the four children. Find the retrieved document ids
on the `retrieve` span. Find the prompt preview on `chat.answer`.

Then read the local `EXP-404` tree. Confirm the tool line says `error`
and the request line says `ok`.

### Exercise 2 — Add the tokens yourself

Option **2**.

Add the `In` column by hand. Add the `Out` column by hand.
Match the `MODEL CALLS` row. Notice `request` is not in the list.

### Exercise 3 — Name the slow step

Option **3**.

Write down the slowest child and its share of the root.
Confirm `retrieve` is near 0%. The search is not the thing you would
speed up first on this corpus.

### Exercise 4 — Check the price, then stuff the prompt

Option **4**.

Before you answer `y`, check the worked example:
1,000 input and 500 output on `gpt-4o` is $0.0075.
Then run the comparison. Input tokens should jump from `chat.short`
to `chat.long`. Output tokens should stay close. The cost line that
moved is the long prompt.

### Exercise 5 — Pay for indexing once

Option **5**. Ask the vacation question.
Note `SESSION SO FAR` against `THIS TRACE`. The gap is the ingest trace
if this was the first model work in the process.

Run option **5** again with "Who answers extension 9911?".
The console should say the handbook is already indexed.
The new tree should have no `ingest.embed`.
The session total should be higher than this trace alone.

### Exercise 6 — Reprice without a new model

On the cost block, read `THIS TRACE` and `IF CHAT WERE gpt-4o`.
The token counts are identical. Only the chat rate changed.
That is why the trace stores tokens, and the price table stays separate.

Now double the `gpt-4o-mini` rates under `Pricing` in `appsettings.json`
and run option **5** again. The tokens barely move; the dollars double.
Put the rates back afterwards.

---

## What's Next?

Once you can point at a trace and say which step failed, which step was
slow, and what the tokens cost, the next chapter is
**Production AI**: security, guardrails, caching, rate limits, and the
checks you run before you deploy. That chapter is `07-ProductionAI.md`,
menu option **11**.
