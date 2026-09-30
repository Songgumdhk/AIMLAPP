// =============================================================================
//  Chapter 6 — Observability Demo (entry point)
// =============================================================================
//
//  PURPOSE
//  -------
//  Show the four pieces of Chapter 6 (see the README roadmap) on one request:
//  Tracing, Token usage, Latency, Cost.
//
//  Folder map (matches 06-Observability.md):
//    Tracing/     Tracer, SpanScope, TraceSpan
//    Usage/       PriceTable, CostCalculator
//    Pipeline/    Handbook, ObservedPipeline
//    ConsoleUi/   TracePrinter
//
//  HOW TO RUN
//  ----------
//  From Program.cs, menu option 10.
// =============================================================================

using System.Text;
using OpenAI.Chat;
using OpenAI.Embeddings;
using AIMLAPP.Learning.ObservabilityDemo.ConsoleUi;
using AIMLAPP.Learning.ObservabilityDemo.Pipeline;
using AIMLAPP.Learning.ObservabilityDemo.Tracing;
using AIMLAPP.Learning.ObservabilityDemo.Usage;

namespace AIMLAPP.Learning.ObservabilityDemo;

public static class ObservabilityDemo
{
    public static async Task RunAsync(string apiKey)
    {
        Console.OutputEncoding = Encoding.UTF8;
        // Checks the $0.0075 example before any API call, so a bad formula
        // fails here instead of printing a wrong bill.
        CostCalculator.VerifyWorkedExample();

        var chat = new ChatClient(ModelNames.Chat, apiKey);
        var embeddings = new EmbeddingClient(ModelNames.Embed, apiKey);
        var handbook = new Handbook();
        Tracer? ingest = null;   // the one-time handbook embedding
        Tracer? last = null;     // the question trace options 2 and 3 reuse
        decimal spent = 0m;      // sum of API traces this process has run

        Console.WriteLine("=== Observability Demo ===");
        Console.WriteLine("Read 06-Observability.md alongside the menu.");
        Console.WriteLine($"Chat: {ModelNames.Chat}. Embeddings: {ModelNames.Embed}.");

        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("--- Pick a piece ---");
            Console.WriteLine("  1) Tracing        — span tree, prompt, output, and a failed tool");
            Console.WriteLine("  2) Token usage    — input, output, and the total you can bill");
            Console.WriteLine("  3) Latency        — which span took the time");
            Console.WriteLine("  4) Cost           — price table, then a short prompt vs a long one");
            Console.WriteLine("  5) Full request   — one question, all four views");
            Console.WriteLine("  0) Quit");
            Console.Write("Selection: ");
            var mode = Console.ReadLine()?.Trim();

            switch (mode)
            {
                case "0":
                    return;
                case "1":
                    last = await RunRequestAsync(chat, embeddings, handbook, () => ingest, t => ingest = t, Remember, () => spent);
                    TracePrinter.PrintTrace(last);
                    Console.WriteLine();
                    Console.WriteLine("A failed tool, built locally so this half does not call a model:");
                    TracePrinter.PrintTrace(SampleErrorTrace());
                    Console.WriteLine("The request span stays ok. The tool span is error. Both statuses have to be stored.");
                    break;
                case "2":
                    last = await EnsureRequestAsync(chat, embeddings, handbook, last, () => ingest, t => ingest = t, Remember, () => spent);
                    TracePrinter.PrintTokens(last);
                    break;
                case "3":
                    last = await EnsureRequestAsync(chat, embeddings, handbook, last, () => ingest, t => ingest = t, Remember, () => spent);
                    TracePrinter.PrintLatency(last);
                    break;
                case "4":
                    await RunCostAsync(chat, last, spent, Remember);
                    break;
                case "5":
                    last = await RunRequestAsync(chat, embeddings, handbook, () => ingest, t => ingest = t, Remember, () => spent);
                    TracePrinter.PrintAll(last, spent);
                    break;
                default:
                    Console.WriteLine("Pick a number from the menu.");
                    break;
            }
        }

        // Called only when a new API trace is created. Reprinting option 2 or 3
        // must not add the same trace to the session total again.
        void Remember(Tracer tracer) => spent += CostCalculator.ForTrace(tracer.Spans);
    }

    private static async Task<Tracer> EnsureRequestAsync(
        ChatClient chat,
        EmbeddingClient embeddings,
        Handbook handbook,
        Tracer? last,
        Func<Tracer?> getIngest,
        Action<Tracer> setIngest,
        Action<Tracer> remember,
        Func<decimal> sessionTotal)
    {
        // Options 2 and 3 read the trace you already paid for.
        if (last is not null)
        {
            Console.WriteLine();
            Console.WriteLine($"Using trace {last.TraceId}. Run option 1 or 5 for a new question.");
            return last;
        }

        return await RunRequestAsync(chat, embeddings, handbook, getIngest, setIngest, remember, sessionTotal);
    }

    private static async Task<Tracer> RunRequestAsync(
        ChatClient chat,
        EmbeddingClient embeddings,
        Handbook handbook,
        Func<Tracer?> getIngest,
        Action<Tracer> setIngest,
        Action<Tracer> remember,
        Func<decimal> sessionTotal)
    {
        if (!handbook.IsReady)
        {
            Console.WriteLine();
            Console.WriteLine("Indexing the handbook once. Later questions in this session skip this trace.");
            var ingestTrace = new Tracer();
            await handbook.IndexAsync(ingestTrace, embeddings);
            setIngest(ingestTrace);
            remember(ingestTrace);
            TracePrinter.PrintTrace(ingestTrace);
            // sessionTotal() is read after Remember, so this line includes the index.
            TracePrinter.PrintCost(ingestTrace, sessionTotal());
        }
        else if (getIngest() is not null)
        {
            Console.WriteLine();
            Console.WriteLine("Handbook already indexed. This request will not repeat ingest.embed.");
        }

        Console.Write($"Question (Enter = {ObservedPipeline.DefaultQuestion}): ");
        var typed = Console.ReadLine();
        var question = string.IsNullOrWhiteSpace(typed) ? ObservedPipeline.DefaultQuestion : typed.Trim();

        Console.WriteLine();
        Console.WriteLine("Running embed → retrieve → tool → chat.");
        var tracer = new Tracer();
        var answer = await ObservedPipeline.AnswerAsync(tracer, chat, embeddings, handbook, question);
        remember(tracer);
        Console.WriteLine();
        Console.WriteLine("Answer: " + answer.Trim());
        return tracer;
    }

    private static async Task RunCostAsync(
        ChatClient chat,
        Tracer? last,
        decimal spent,
        Action<Tracer> remember)
    {
        TracePrinter.PrintPriceTable();
        if (last is not null)
        {
            Console.WriteLine();
            Console.WriteLine($"Last request trace {last.TraceId}:");
            TracePrinter.PrintCost(last, spent);
        }

        Console.WriteLine();
        Console.WriteLine("Compare a one-line prompt with the same question plus a long handbook? Two chat calls.");
        Console.Write("Run it? [y/n]: ");
        var yes = Console.ReadLine()?.Trim().ToLowerInvariant();
        if (yes is not ("y" or "yes")) return;

        var tracer = new Tracer();
        await ObservedPipeline.ComparePromptLengthAsync(tracer, chat);
        // `spent` is a copy from when this method started. Add this trace
        // for the printed session line. Remember updates the real session.
        var comparison = CostCalculator.ForTrace(tracer.Spans);
        remember(tracer);
        TracePrinter.PrintTokens(tracer);
        TracePrinter.PrintCost(tracer, spent + comparison);

        var shortSpan = tracer.Spans.Single(s => s.Name == "chat.short");
        var longSpan = tracer.Spans.Single(s => s.Name == "chat.long");
        Console.WriteLine();
        Console.WriteLine(
            $"Input tokens moved from {shortSpan.InputTokens} to {longSpan.InputTokens}. Output stayed near {shortSpan.OutputTokens} and {longSpan.OutputTokens}.");
        Console.WriteLine("The extra cost is the handbook you stuffed into the prompt, not a longer answer.");
    }

    // No model call. Shows a child can be error while the parent stays ok.
    // WHY: a failed tool and a finished request are different facts. A catch
    // block that logs one line hides that; the span tree keeps both (§2).
    private static Tracer SampleErrorTrace()
    {
        var tracer = new Tracer();
        using (var request = tracer.Begin("request", "server"))
        {
            request.SetDetail("What does policy EXP-404 cover?");
            using var tool = tracer.Begin("tool.lookup_policy", "tool");
            tool.Fail("EXP-404 is not in the handbook.");
            tool.SetDetail("EXP-404");
        }

        return tracer;
    }
}
