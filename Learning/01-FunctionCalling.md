# Chapter 1 — Function Calling / Tool Calling (Deep Dive)

> **Goal:** Understand function calling **without a framework** so that when you later use Semantic Kernel, LangChain, or Microsoft.Agents.AI, you know *exactly* what they are automating for you.

---

## Table of Contents

1. [What is Function Calling?](#1-what-is-function-calling)
2. [The Core Loop](#2-the-core-loop)
3. [Tool / Function Schemas](#3-tool--function-schemas)
4. [Tool Selection (`tool_choice`)](#4-tool-selection-tool_choice)
5. [Arguments](#5-arguments)
6. [Multiple Tool Calls (Sequential)](#6-multiple-tool-calls-sequential)
7. [Parallel Tool Calls](#7-parallel-tool-calls)
8. [Tool Errors](#8-tool-errors)
9. [Retry Strategies](#9-retry-strategies)
10. [Human Approval / Human-in-the-Loop](#10-human-approval--human-in-the-loop)
11. [Common Pitfalls](#11-common-pitfalls)
12. [Practice Exercises](#12-practice-exercises)

> **Settings for this chapter** live in `appsettings.json`, not in code:
> `OpenAI:ChatModel` (the model that picks tools) and `FunctionCalling:MaxTurns`
> (the default loop cap). Your API key goes in `appsettings.Local.json`.
> See the README's Configuration section.

---

## 1. What is Function Calling?

**Definition:** Function calling (also called *tool calling*) is a feature of modern LLMs where the model can, in its response, **request that your application execute a function** on its behalf, then use the result to continue the conversation.

### Why it matters

An LLM by itself is a **text predictor**. It cannot:

- Access your database
- Call an external API
- Do reliable math
- Read a file
- Send an email

Function calling is the **bridge** that turns an LLM into an *application*:

```
LLM (thinks) → Tool (acts) → LLM (interprets result) → User (sees answer)
```

This is literally what every "AI Agent" does under the hood. When you used the `Microsoft.Agents.AI` package, it was doing this loop for you invisibly.

---

## 2. The Core Loop

Function calling is a **conversation loop**, not a single request. Here is the flow:

```
┌─────────────────────────────────────────────────────────┐
│ 1. You send: messages + list of available tools        │
└─────────────────────────────────────────────────────────┘
                        ↓
┌─────────────────────────────────────────────────────────┐
│ 2. LLM responds with EITHER:                            │
│    a) Final text answer   → done                        │
│    b) Tool call request   → continue                    │
└─────────────────────────────────────────────────────────┘
                        ↓ (if tool call)
┌─────────────────────────────────────────────────────────┐
│ 3. YOUR CODE executes the requested function            │
│    (LLM does NOT run any code — you do)                 │
└─────────────────────────────────────────────────────────┘
                        ↓
┌─────────────────────────────────────────────────────────┐
│ 4. You append the tool result to the conversation       │
│    and call the LLM AGAIN                               │
└─────────────────────────────────────────────────────────┘
                        ↓
                  (back to step 2)
```

**Key insight:** The LLM never executes anything. It only *requests* a call and *reads* the result you feed back. You are always in control.

---

## 3. Tool / Function Schemas

A **tool schema** is a JSON description that tells the LLM:

- The tool's **name**
- What it **does** (description)
- What **arguments** it accepts (parameter types)

### Example (JSON Schema)

```json
{
  "type": "function",
  "function": {
    "name": "get_weather",
    "description": "Get the current weather in a given city.",
    "parameters": {
      "type": "object",
      "properties": {
        "city":  { "type": "string", "description": "City name, e.g. Sydney" },
        "unit":  { "type": "string", "enum": ["celsius", "fahrenheit"] }
      },
      "required": ["city"]
    }
  }
}
```

### Why the `description` matters more than the `name`

The LLM decides *whether* to call your tool by reading the **description**. A bad description = a bad agent.

**Bad:**
```json
"description": "Weather tool"
```

**Good:**
```json
"description": "Get the current temperature and conditions for a city. Use this whenever the user asks about weather, temperature, rain, or forecast for any location."
```

Rules of thumb:
- Describe **when** to use it, not just what it does.
- Describe every parameter clearly.
- Use `enum` for fixed choices — this constrains the LLM.
- Mark required fields with `required: [...]`.

---

## 4. Tool Selection (`tool_choice`)

You can control **how eagerly** the LLM uses tools:

| Setting             | Behavior                                                        |
| ------------------- | --------------------------------------------------------------- |
| `"auto"` (default)  | LLM decides whether to call a tool or answer directly.          |
| `"none"`            | Force the LLM to answer with text only.                         |
| `"required"`        | Force the LLM to call *some* tool (any of them).                |
| `{name: "tool_x"}`  | Force the LLM to call a *specific* tool.                        |

**When to force?** Rarely. `"auto"` is best 95% of the time. Force a specific tool only when you're building a deterministic pipeline (e.g., "always extract entities first").

---

## 5. Arguments

When the LLM calls a tool, it produces a **JSON string** of arguments that matches your schema:

```json
{ "city": "Sydney", "unit": "celsius" }
```

### Things to watch for

1. **It's a string** — always deserialize with `JsonDocument` or `JsonSerializer`.
2. **The LLM can hallucinate fields** — validate against your schema.
3. **Optional fields may be missing** — provide sensible defaults.
4. **Types may be wrong** — the LLM sometimes sends `"5"` instead of `5`. Handle with tolerance or `TryParse`.

---

## 6. Multiple Tool Calls (Sequential)

Real tasks require **chains** of tool calls:

> *"Book me a flight to Tokyo next Friday."*

The LLM might:

1. Call `get_current_date()` → learns today is Sep 20
2. Call `search_flights(destination: "Tokyo", date: "2026-09-25")` → gets 5 flights
3. Call `book_flight(flight_id: "JAL432")` → confirms booking
4. Reply to user: "Booked JAL432, departing Friday 25 Sep."

Each step is **one full round-trip** through the loop from Section 2. Your code must keep looping until the LLM stops requesting tools (`FinishReason != ToolCalls`).

```csharp
while (completion.FinishReason == ChatFinishReason.ToolCalls)
{
    // execute all tool calls, feed results back
    completion = await client.CompleteChatAsync(messages, options);
}
```

---

## 7. Parallel Tool Calls

Modern models (GPT-4o, GPT-4-turbo, Claude) can return **multiple tool calls in a single response** when they know the calls are independent:

> *"What's the weather in Sydney, Tokyo, and New York?"*

Response contains **3 tool calls at once**. Execute them **in parallel** for speed:

```csharp
var tasks = completion.ToolCalls.Select(tc => ExecuteToolAsync(tc));
var results = await Task.WhenAll(tasks);
```

**Warning:** If the calls have dependencies (e.g., call B needs output from call A), the LLM will *not* parallelize them — it will do them sequentially across multiple round-trips.

---

## 8. Tool Errors

Your tools **will fail**. Network timeouts, invalid IDs, permission denied, etc.

### Do NOT throw the exception up

If you throw, your app crashes and the LLM never learns what went wrong.

### DO feed the error back as the tool result

```csharp
try
{
    result = await ExecuteToolAsync(toolCall);
}
catch (Exception ex)
{
    result = $"ERROR: {ex.Message}";
}
messages.Add(new ToolChatMessage(toolCall.Id, result));
```

The LLM will read the error and can:
- Retry with different arguments
- Ask the user for clarification
- Give up gracefully and explain

---

## 9. Retry Strategies

Three levels of retry:

| Level                   | Who retries       | When                                    |
| ----------------------- | ----------------- | --------------------------------------- |
| **HTTP layer**          | Polly / SDK       | Transient network faults (5xx, 429)     |
| **Tool layer**          | Your code         | Deterministic tool errors (DB deadlock) |
| **LLM layer**           | The model itself  | Feed error back, let LLM try again      |

**Rule:** Cap the LLM retry loop! Otherwise a broken tool can burn tokens forever.

```csharp
const int MAX_TURNS = 8;
for (int turn = 0; turn < MAX_TURNS && completion.FinishReason == ChatFinishReason.ToolCalls; turn++)
{
    // execute and loop
}
```

In this repo the cap is not a `const`. The default comes from
`FunctionCalling:MaxTurns` in `appsettings.json`, so you can tune it without
recompiling.

---

## 10. Human Approval / Human-in-the-Loop

Some tools are **dangerous**:
- Send email
- Charge credit card
- Delete records
- Deploy to production

For these, **pause the loop** before executing:

```
LLM requests: send_email(to: "boss@x.com", body: "...")
     ↓
Your code detects this is a "dangerous" tool
     ↓
Show user: "Agent wants to send an email. Approve? [y/n]"
     ↓
If YES → execute → feed result back
If NO  → feed back "User denied approval" → LLM adapts
```

This is called **HITL (Human-in-the-Loop)** and is a mandatory pattern for production AI systems.

---

## 11. Common Pitfalls

| Pitfall                                                      | Fix                                                                             |
| ------------------------------------------------------------ | ------------------------------------------------------------------------------- |
| Forgetting to add the **assistant message** back to history  | Always add `new AssistantChatMessage(completion)` before adding tool results.   |
| Adding tool results **without** their `toolCallId`           | The `ToolChatMessage` MUST reference the exact `Id` from the assistant message. |
| Infinite loop (LLM keeps calling the same broken tool)       | Cap `MAX_TURNS`.                                                                |
| Tool descriptions too vague                                  | Rewrite them focusing on **when** to use.                                       |
| Not validating arguments                                     | Always `JsonDocument.Parse` + null-check every property.                        |
| Ignoring parallel calls                                      | Loop over `completion.ToolCalls` — don't just take the first.                   |
| Using `tool_choice: required` everywhere                     | Leave it as `auto` unless you have a specific reason.                           |

---

## 12. Practice Exercises

> **All 5 exercises are already wired into `FunctionCallingDemo.cs`.**
> You don't need to edit code — the demo now asks you at startup for
> `MAX_TURNS` (Enter keeps the default from `FunctionCalling:MaxTurns`) and a
> `tool_choice` mode, so you can trigger every exercise just by picking
> different prompts. Run it with `dotnet run` → option 5.

### Exercise 1 — Parallel tool calls with a new tool

**What was added:** a `get_stock_price(symbol)` tool with a description that
explicitly invites parallel calls.

**Try:**
```
What are the prices of MSFT and GOOG?
```

**Look for:** the log line `[Turn 1/8] LLM requested 2 tool call(s)` —
proving both stock lookups came back in a **single** assistant response and
ran in parallel via `Task.WhenAll`.

**Contrast with:**
```
What's the price of MSFT?
```
→ only 1 tool call in Turn 1.

---

### Exercise 2 — Tool error handling

**What was added:** `GetWeatherAsync` throws for `city == "Atlantis"`. The
`try/catch` in `ExecuteToolCallAsync` converts the exception into a string
result the LLM can read.

**Try:**
```
What's the weather in Atlantis?
```

**Look for:**
1. Turn 1: `get_weather({"city":"Atlantis"}) = ERROR: InvalidOperationException: City 'Atlantis' not found...`
2. Turn 2: the LLM reads the error and either apologizes to the user OR asks *"Did you mean a different city?"*

**Try also:**
```
What's the weather in Atlantis and Sydney?
```
→ Sydney succeeds, Atlantis fails. Watch how the LLM cleanly reports mixed
results in one final answer. This is why you **never throw** out of a tool.

---

### Exercise 3 — Forcing a specific tool

**What was added:** the startup prompt lets you pick tool-choice mode.

**Try:** restart the demo, at the prompt select `2` (force `search_database`).
Then type:
```
Hi
```

**Look for:** even though "Hi" has nothing to do with employees, the LLM is
**forced** to call `search_database` and will hallucinate an argument like
`{"department":"Engineering"}`.

**Lesson:** forcing tools removes the LLM's judgment. Use `auto` unless you
have a specific reason (e.g., a deterministic pipeline step).

**Also try mode `3` (none):**
```
What's the weather in Sydney?
```
→ The LLM sees `get_weather` in the catalog but is forbidden from calling
it, so it answers in plain text ("I don't have live weather data...").

---

### Exercise 4 — Human-in-the-Loop for destructive tools

**What was added:** a `delete_record(table, id)` tool, gated by
`RequiresHumanApproval`.

**Try:**
```
Delete record 42 from the customers table.
```

**Look for:**
```
[HUMAN APPROVAL REQUIRED]
  Tool: delete_record
  Args: {"table":"customers","id":42}
  Approve? (y/n):
```

Type `n`. The tool result becomes `"DENIED: The user declined..."`, and the
LLM will politely stop (it won't retry immediately because the message
explicitly tells it not to).

Type `y` on a re-run and it executes.

**Combine with Exercise 5:** try
```
Find the HR director and delete their user record.
```
→ Turn 1: `search_database`, Turn 2: `delete_record` (approval prompt).

---

### Exercise 5 — Capping the loop

**What was added:** the startup prompt lets you set `MAX_TURNS`. When the
cap trips, you get a loud warning instead of a silent exit.

**Try:** restart the demo, set `MAX_TURNS = 1`. Then ask:
```
Find the HR director's email and send them a note asking about vacation policy.
```

This task **requires 3 sequential tool calls** (the LLM cannot parallelize
them because each depends on the previous):
1. `search_database({department:"HR"})` → `"Frank (Director)"`
2. `get_employee_email({name:"Frank"})` → `"frank@company.com"`
3. `send_email({to:"frank@company.com", ...})` → approval → sent

**Look for:** with `MAX_TURNS=1`, the loop stops after step 1 with:
```
[!!] MAX_TURNS (1) reached without a final answer.
     The LLM is either stuck in a loop or needs more turns.
     Restart the demo with a higher MAX_TURNS to see it succeed.
```

Restart with `MAX_TURNS=8` and the same prompt succeeds through all 3 turns.

To change what Enter picks, edit `FunctionCalling:MaxTurns` in
`appsettings.json`.

**Lesson:** always cap the loop, but tune the cap to the complexity of
tasks you support.

---

## What's Next?

Once every exercise above feels *obvious* to you, you're ready for **Chapter 2: Semantic Kernel** — where all of this becomes a couple of attributes on your C# methods.

You'll appreciate SK *much* more after doing this the hard way. That's the point.
