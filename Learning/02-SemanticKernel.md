# Chapter 2 — Semantic Kernel (.NET)

> **Goal:** Learn Semantic Kernel by seeing exactly how it *replaces* every
> hand-written piece from Chapter 1. If Chapter 1 was the engine, SK is the
> car — same physics, better ergonomics.

---

## Table of Contents

1. [What is Semantic Kernel?](#1-what-is-semantic-kernel)
2. [Why use it over the raw OpenAI SDK?](#2-why-use-it-over-the-raw-openai-sdk)
3. [Core Concepts](#3-core-concepts)
4. [Chapter 1 → Semantic Kernel Mapping](#4-chapter-1--semantic-kernel-mapping)
5. [Plugins & `[KernelFunction]`](#5-plugins--kernelfunction)
6. [Auto vs Manual Function Invocation](#6-auto-vs-manual-function-invocation)
7. [Filters — Middleware for AI](#7-filters--middleware-for-ai)
8. [Prompt Templates](#8-prompt-templates)
9. [Chat History and Memory](#9-chat-history-and-memory)
10. [Model Providers (OpenAI, Azure, Ollama, ONNX)](#10-model-providers)
11. [Common Pitfalls](#11-common-pitfalls)
12. [Practice Exercises](#12-practice-exercises)

> **Settings for this chapter** live in `appsettings.json`: `OpenAI:ChatModel`
> is the model passed to `AddOpenAIChatCompletion`. Your API key goes in
> `appsettings.Local.json`. See the README's Configuration section.

---

## 1. What is Semantic Kernel?

**Semantic Kernel (SK)** is Microsoft's open-source AI orchestration SDK for
.NET, Python, and Java. It sits between your app and the LLM and handles the
"agent plumbing" you built by hand in Chapter 1:

- The message ↔ tool ↔ result loop
- JSON schema generation for tools
- Cross-cutting concerns (logging, telemetry, security)
- Multiple model providers (OpenAI, Azure OpenAI, Ollama, Hugging Face, ONNX)

Think of SK as **ASP.NET Core for AI**: it gives you DI, middleware
(filters), a clean plugin model, and swappable providers.

---

## 2. Why use it over the raw OpenAI SDK?

You just spent Chapter 1 doing it "by hand." What did that give you?

| You wrote                                | SK does automatically                           |
| ---------------------------------------- | ----------------------------------------------- |
| JSON schemas as `BinaryData` strings     | Generated from method signatures + attributes   |
| The `for (turn < MAX_TURNS)` loop        | Handled by `FunctionChoiceBehavior.Auto()`      |
| The `switch` dispatcher                  | Automatic, by function name                     |
| Manual `ToolChatMessage` bookkeeping     | Handled                                         |
| Human-approval gate                      | Cleaner via **Filters**                         |
| Error → string conversion                | Built in                                        |
| Swapping OpenAI ↔ Azure ↔ Ollama         | Change 1 line in the builder                    |

**Trade-off:** SK adds a small abstraction cost. You lose some control over
message shape (rarely a problem). In exchange you get ~70% less code and
built-in observability.

**Rule of thumb:**
- **Learning / debugging protocol** → raw SDK (Chapter 1)
- **Building anything real** → SK (Chapter 2)

---

## 3. Core Concepts

Five things to master:

| Concept                    | What it is                                                                |
| -------------------------- | ------------------------------------------------------------------------- |
| `Kernel`                   | The DI container + config for your AI app.                                |
| **Plugin**                 | A C# class exposing 1+ methods decorated with `[KernelFunction]`.         |
| `[KernelFunction]`         | Attribute that turns a normal C# method into a tool the LLM can call.    |
| `IChatCompletionService`   | The interface for talking to any LLM (OpenAI, Azure, Ollama, etc.).       |
| `FunctionChoiceBehavior`   | Controls tool selection — `Auto()`, `Required()`, `None()`.               |
| **Filters**                | Middleware that runs before/after prompts, function calls, or LLM calls.  |

---

## 4. Chapter 1 → Semantic Kernel Mapping

The two demos in `Learning/` implement the **same 6 tools**. Here's exactly
how each concept translates:

### 4.1 A tool

**Chapter 1** (raw SDK):

```csharp
ChatTool.CreateFunctionTool(
    functionName: "get_weather",
    functionDescription: "Get the current weather for a city...",
    functionParameters: BinaryData.FromString("""
    {
        "type": "object",
        "properties": {
            "city": { "type": "string", "description": "..." }
        },
        "required": ["city"]
    }
    """)
);

private static Task<string> GetWeatherAsync(JsonElement args)
{
    var city = args.GetProperty("city").GetString();
    // ...
}
```

**Chapter 2** (Semantic Kernel):

```csharp
public class WeatherPlugin
{
    [KernelFunction("get_weather")]
    [Description("Get the current weather and temperature for a city.")]
    public string GetWeather(
        [Description("City name, e.g. 'Sydney'.")] string city,
        [Description("celsius or fahrenheit")] string unit = "celsius")
    {
        // just C# — no JSON parsing, no manual dispatch
    }
}
```

SK generates the JSON schema from the method signature and attributes. **No
JSON string literals. No `JsonDocument.Parse`.** The `city` argument is
already typed as `string`.

### 4.2 The loop

**Chapter 1:**

```csharp
for (int turn = 0; turn < MAX_TURNS; turn++)
{
    var completion = await client.CompleteChatAsync(messages, options);
    if (completion.FinishReason == ChatFinishReason.Stop) break;
    // ... add assistant, execute tools in parallel, add tool results, loop
}
```

**Chapter 2:**

```csharp
var settings = new OpenAIPromptExecutionSettings
{
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
};
var result = await chatService.GetChatMessageContentAsync(history, settings, kernel);
```

**One line.** SK runs the entire loop for you — including every tool call the
model requests, error → string conversion, and looping until final answer.

> **Parallel calls:** the model can still request several tools in one turn,
> but by default SK invokes them one after another. To run them concurrently,
> opt in with
> `FunctionChoiceBehavior.Auto(options: new() { AllowConcurrentInvocation = true })`.

### 4.3 Human-in-the-loop

**Chapter 1:** if-else check inside `ExecuteToolCallAsync`.

**Chapter 2:** implement `IFunctionInvocationFilter`:

```csharp
public class ApprovalFilter : IFunctionInvocationFilter
{
    public async Task OnFunctionInvocationAsync(
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, Task> next)
    {
        if (RequiresApproval(context.Function.Name))
        {
            Console.Write($"Approve {context.Function.Name}? (y/n): ");
            if (Console.ReadLine() != "y")
            {
                context.Result = new FunctionResult(context.Function, "DENIED by user");
                return; // don't call next() — skip the real function
            }
        }
        await next(context); // proceed
    }
}
```

Register once, applies to **all** functions. This is much cleaner than
inline checks.

---

## 5. Plugins & `[KernelFunction]`

A **plugin** is just a plain C# class:

```csharp
public class StocksPlugin
{
    [KernelFunction("get_stock_price")]
    [Description("Get the current price for a stock ticker symbol.")]
    public string GetPrice(
        [Description("Ticker symbol, e.g. 'MSFT'.")] string symbol)
    {
        return $"${LookupPrice(symbol):F2}";
    }
}
```

### Rules

- Methods can be **sync or async**.
- Return type can be anything JSON-serializable (`string`, `int`, DTOs, `Task<T>`).
- Parameters need `[Description(...)]` for the LLM to understand them.
- Method name can be overridden with `[KernelFunction("name_here")]`.
- Multiple plugins are fine — SK combines them into one tool catalog.

### Registering plugins

```csharp
var builder = Kernel.CreateBuilder();
builder.AddOpenAIChatCompletion(AppSettings.Current.OpenAI.ChatModel, apiKey); // "gpt-4o" by default
Kernel kernel = builder.Build();

kernel.Plugins.AddFromType<WeatherPlugin>();       // stateless plugin
kernel.Plugins.AddFromObject(new HrPlugin(dbConn)); // needs a state/dep
```

---

## 6. Auto vs Manual Function Invocation

SK gives you three tool-invocation modes via `FunctionChoiceBehavior`:

### Auto (recommended, matches Chapter 1's "auto")

```csharp
var settings = new OpenAIPromptExecutionSettings
{
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
};
```

SK runs the whole loop for you. This is 99% of use cases.

### Required (force *some* tool)

```csharp
FunctionChoiceBehavior = FunctionChoiceBehavior.Required()
```

Matches Chapter 1's `tool_choice: "required"`.

### None (forbid tools, force text answer)

```csharp
FunctionChoiceBehavior = FunctionChoiceBehavior.None()
```

Matches Chapter 1's `tool_choice: "none"`. Useful for a "final answer"
follow-up call after your MAX_TURNS trips.

### Auto with manual invocation

You can also ask SK to give you the tool calls to run yourself (like
Chapter 1) via `FunctionChoiceBehavior.Auto(autoInvoke: false)`. Rare — use
it when you need custom parallelism control.

---

## 7. Filters — Middleware for AI

This is SK's killer feature. Filters are middleware that intercept:

- **Prompt rendering** — before templates are filled in (`IPromptRenderFilter`)
- **Function invocation** — before/after each tool call (`IFunctionInvocationFilter`)
- **Automatic function choice** — before/after LLM tool decisions (`IAutoFunctionInvocationFilter`)

Real-world uses:

- **Human approval** (as shown above)
- **Logging + tracing** (OpenTelemetry span per tool call)
- **PII redaction** (mask emails/SSNs before they hit the LLM)
- **Rate limiting** (throttle tool calls per user)
- **Prompt injection defense** (scan inputs, block suspicious patterns)
- **Cost tracking** (accumulate token spend per user)

Registration:

```csharp
builder.Services.AddSingleton<IFunctionInvocationFilter, ApprovalFilter>();
builder.Services.AddSingleton<IFunctionInvocationFilter, LoggingFilter>();
```

Filters run in registration order. This is directly analogous to ASP.NET
Core middleware.

---

## 8. Prompt Templates

SK has a built-in template engine so you can factor prompts into reusable,
parameterized units. Two syntaxes:

### Inline

```csharp
var summarize = kernel.CreateFunctionFromPrompt(
    "Summarize this text in 3 bullet points:\n\n{{$input}}"
);

var result = await kernel.InvokeAsync(summarize, new() { ["input"] = article });
```

### File-based (for larger prompts)

```
Prompts/
└── SummarizePlugin/
    └── Summarize/
        ├── skprompt.txt          <- the template
        └── config.json           <- parameters + description
```

```csharp
kernel.Plugins.AddFromPromptDirectory("Prompts");
```

Templates can reference:
- `{{$input}}` — a variable
- `{{plugin.function $arg}}` — invoke another plugin
- `{{#if}}...{{/if}}` — conditionals (Handlebars flavor)

You **cannot** do this with the raw OpenAI SDK. It's a big productivity win.

---

## 9. Chat History and Memory

SK provides `ChatHistory` — a strongly-typed message list:

```csharp
var history = new ChatHistory();
history.AddSystemMessage("You are a helpful assistant.");
history.AddUserMessage("What's the weather in Sydney?");

var reply = await chatService.GetChatMessageContentAsync(history, settings, kernel);
history.Add(reply); // saves the final assistant answer
```

With auto-invoke, SK appends the intermediate tool-call and tool-result
messages to `history` itself while the call runs. `history.Add(reply)` then
adds only the final answer. Together they replace Chapter 1's manual
bookkeeping:
- `messages.Add(new AssistantChatMessage(completion))`
- `messages.Add(new ToolChatMessage(...))` × N

For long conversations, you'll want to *summarize* older turns to stay under
the token limit. That's a topic for Chapter 3 (Advanced RAG) — SK has
built-in summarization plugins.

---

## 10. Model Providers

The **single biggest reason** enterprise .NET shops adopt SK: **provider
independence**.

Same code, different backends:

```csharp
builder.AddOpenAIChatCompletion("gpt-4o", apiKey);                       // OpenAI
builder.AddAzureOpenAIChatCompletion("gpt-4o-deployment", endpoint, key); // Azure
builder.AddOllamaChatCompletion("llama3.1", endpoint);                    // Local
```

Everything downstream — plugins, filters, prompts, history — is identical.
This matters when:

- Dev uses OpenAI, prod uses Azure OpenAI (compliance)
- You want an offline fallback via Ollama
- Different customers require different models (data residency)

Keep the model names and endpoints in configuration rather than in these
calls. This repo reads them from `appsettings.json` (`OpenAI:ChatModel`,
`Ollama:BaseUrl`, `Ollama:ChatModel`), so switching models is a config
change, and switching providers is one line of code.

Chapter 8 (Local AI) leans on this heavily.

---

## 11. Common Pitfalls

| Pitfall                                                    | Fix                                                                  |
| ---------------------------------------------------------- | -------------------------------------------------------------------- |
| Forgetting `[Description]` on parameters                   | LLM sees `arg0`, `arg1` — no idea what they mean. Always describe.   |
| Registering a plugin with **state** as `AddFromType<T>`    | SK instantiates a new object every call; state is lost. Use `AddFromObject(...)`. |
| Throwing exceptions from `[KernelFunction]` methods        | Actually OK — SK catches and feeds error strings back. But log it.    |
| Filter that forgets to call `next(context)`                | Silently blocks all downstream logic. Always call `next` (or set `context.Result` to skip). |
| Mixing `Auto()` with manual `kernel.InvokeAsync` in a loop | Pick one style. Auto is the loop; manual is a single call.           |
| `FunctionChoiceBehavior` set on the wrong settings object  | Must be on the `PromptExecutionSettings` passed to the chat service. |

---

## 12. Practice Exercises

All exercises run from the demo (Program menu option **6**).

### Exercise 1 — Verify SK really runs the loop for you

**Run:** menu option 6, prompt:
```
What's the weather in Sydney, Tokyo, and Paris?
```

Notice you get one final answer immediately. There is **no** `for (turn...)`
loop in the demo — SK ran it silently. Enable the logging filter (default:
on) to see how many function calls happened.

### Exercise 2 — Compare error handling

Ask:
```
What's the weather in Atlantis?
```

Same behavior as Chapter 1: SK catches the exception, feeds a string back to
the LLM, LLM apologizes gracefully. **No try/catch anywhere in your plugin.**

### Exercise 3 — Filter-based human approval

Ask:
```
Delete record 42 from the customers table.
```

Watch the `ApprovalFilter` intercept. Deny it. Observe how much cleaner this
is than the inline gate in Chapter 1.

### Exercise 4 — Switch providers

Change `OpenAI:ChatModel` from `"gpt-4o"` to `"gpt-4o-mini"` in
`appsettings.json` (the demo passes it to `AddOpenAIChatCompletion`).
Rerun. Nothing else changes — same plugins, same filters. That's the value
of SK.

### Exercise 5 — Prompt template

Add a new prompt-only function:

```csharp
var summarize = kernel.CreateFunctionFromPrompt(
    "Summarize this in one sentence: {{$input}}"
);
var result = await kernel.InvokeAsync(summarize, new() { ["input"] = "..." });
```

Notice: no LLM tool call — this is a plain template invocation. You just
built a reusable prompt as a first-class function.

---

## What's Next?

Once every exercise here feels obvious, move on to **Chapter 3: Advanced RAG**
(`03-AdvancedRAG.md`, menu option 7). SK has built-in RAG helpers (`ITextSearch`, memory
connectors, semantic chunking) so you'll build on this foundation.
