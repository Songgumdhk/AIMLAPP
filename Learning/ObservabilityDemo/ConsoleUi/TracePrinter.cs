using System.Globalization;
using AIMLAPP.Learning.ObservabilityDemo.Tracing;
using AIMLAPP.Learning.ObservabilityDemo.Usage;

namespace AIMLAPP.Learning.ObservabilityDemo.ConsoleUi;

// Prints the four views of one trace: tree, tokens, latency, cost.
// It only reads stored span fields. It never calls a model.
public static class TracePrinter
{
    public static void PrintTrace(Tracer tracer)
    {
        Console.WriteLine();
        Console.WriteLine($"trace {tracer.TraceId}");
        // A missing parent id means this span is a root of the tree.
        foreach (var root in tracer.Spans.Where(s => s.ParentId is null))
            PrintSpan(tracer, root, 0);
    }

    public static void PrintTokens(Tracer tracer)
    {
        Console.WriteLine();
        Console.WriteLine("Token usage — only spans that called a model. The request span is not in this list.");
        Console.WriteLine($"{"Span",-22} {"Model",-24} {"In",8} {"Out",8} {"Total",8}");

        // IsModelCall is true only after RecordChat or RecordEmbed set Model.
        var leaves = tracer.Spans.Where(s => s.IsModelCall).ToList();
        foreach (var span in leaves)
        {
            Console.WriteLine(
                $"{span.Name,-22} {span.Model,-24} {span.InputTokens,8} {span.OutputTokens,8} {span.InputTokens + span.OutputTokens,8}");
            if (span.CachedInputTokens > 0)
                Console.WriteLine($"{"",-22} cached input {span.CachedInputTokens} (already inside the In column, not extra)");
        }

        var input = leaves.Sum(s => s.InputTokens);
        var output = leaves.Sum(s => s.OutputTokens);
        Console.WriteLine($"{"MODEL CALLS",-22} {"",-24} {input,8} {output,8} {input + output,8}");
        Console.WriteLine();
        Console.WriteLine("Input tokens are the prompt you sent, including retrieved text.");
        Console.WriteLine("Output tokens are what the model wrote back. Embeddings have output 0.");
        Console.WriteLine("Adding the parent request on top of these rows would bill the same calls twice.");
    }

    public static void PrintLatency(Tracer tracer)
    {
        var roots = tracer.Spans.Where(s => s.ParentId is null).ToList();
        if (roots.Count == 0) return;

        // Shift start times so the first span prints as 0 ms.
        var origin = tracer.Spans.Min(s => s.StartMs);
        var rootDuration = roots.Max(s => s.DurationMs);
        // A zero-length local trace would divide the share column by zero.
        if (rootDuration <= 0) rootDuration = 1;

        Console.WriteLine();
        Console.WriteLine("Latency — start is milliseconds from the first span. Share is of the root duration.");
        Console.WriteLine($"{"Span",-24} {"Start",8} {"Duration",10} {"Share",8}");

        foreach (var root in roots)
            PrintLatencyRow(tracer, root, origin, rootDuration, 0);

        // Skip the root. It is the whole request, so it would always "win".
        var slowest = tracer.Spans
            .Where(s => s.ParentId is not null)
            .OrderByDescending(s => s.DurationMs)
            .FirstOrDefault();
        if (slowest is not null)
        {
            var share = slowest.DurationMs / (double)rootDuration;
            Console.WriteLine();
            Console.WriteLine(
                $"Slowest step: {slowest.Name} at {slowest.DurationMs} ms ({share.ToString("0%", CultureInfo.InvariantCulture)} of the root).");
            Console.WriteLine("A single request-time log would hide which step that was.");
        }
    }

    public static void PrintCost(Tracer tracer, decimal sessionTotal)
    {
        Console.WriteLine();
        Console.WriteLine("Cost — each model span priced from PriceTable. Parents add $0.");
        Console.WriteLine($"{"Span",-22} {"Model",-24} {"Cost",14}");

        foreach (var span in tracer.Spans.Where(s => s.IsModelCall))
            Console.WriteLine($"{span.Name,-22} {span.Model,-24} {Money(CostCalculator.ForSpan(span)),14}");

        // Same measured tokens, two rate cards. Only the chat rate changes between lines.
        var actual = CostCalculator.ForTrace(tracer.Spans);
        var full = PriceTable.Chat;
        var asFull = CostCalculator.ChatAs(tracer.Spans, full);
        Console.WriteLine($"{"THIS TRACE",-22} {"",-24} {Money(actual),14}");
        Console.WriteLine($"{"IF CHAT WERE " + full.Model,-22} {"",-24} {Money(asFull),14}");
        Console.WriteLine($"{"SESSION SO FAR",-22} {"",-24} {Money(sessionTotal),14}");
        Console.WriteLine();
        Console.WriteLine("On these two chat models, one output token costs the same as four input tokens.");
        Console.WriteLine($"The {full.Model} line uses the token counts you just measured. It does not call {full.Model}.");
    }

    public static void PrintPriceTable()
    {
        Console.WriteLine();
        Console.WriteLine("Price per 1M tokens (from appsettings.json Pricing, full input price):");
        PrintPrice(PriceTable.Chat);
        PrintPrice(PriceTable.SmallChat);
        PrintPrice(PriceTable.Embedding);
        Console.WriteLine();
        Console.WriteLine("Worked example: 1,000 input tokens and 500 output tokens.");
        Console.WriteLine($"  {PriceTable.Chat.Model,-20} {Money(PriceTable.Chat.Cost(1000, 500))}");
        Console.WriteLine($"  {PriceTable.SmallChat.Model,-20} {Money(PriceTable.SmallChat.Cost(1000, 500))}");
        Console.WriteLine($"  embedding, 1,000 in  {Money(PriceTable.Embedding.Cost(1000, 0))}");
        Console.WriteLine("gpt-4o input: 1000 / 1,000,000 * $2.50 = $0.0025");
        Console.WriteLine("gpt-4o output: 500 / 1,000,000 * $10.00 = $0.0050");
        Console.WriteLine("total $0.0075");
    }

    public static void PrintAll(Tracer tracer, decimal sessionTotal)
    {
        PrintTrace(tracer);
        PrintTokens(tracer);
        PrintLatency(tracer);
        PrintCost(tracer, sessionTotal);
    }

    private static void PrintSpan(Tracer tracer, TraceSpan span, int depth)
    {
        var pad = new string(' ', depth * 2);
        var status = span.Status == "error" ? "error" : "ok";
        Console.WriteLine($"{pad}{span.Name,-22} {span.DurationMs,6} ms  {status,-5}  {span.Kind}");
        if (!string.IsNullOrWhiteSpace(span.Detail))
            Console.WriteLine($"{pad}  {span.Detail}");
        if (!string.IsNullOrWhiteSpace(span.Error))
            Console.WriteLine($"{pad}  error: {span.Error}");
        if (!string.IsNullOrWhiteSpace(span.PromptPreview))
            Console.WriteLine($"{pad}  prompt: {span.PromptPreview}");
        if (!string.IsNullOrWhiteSpace(span.OutputPreview))
            Console.WriteLine($"{pad}  output: {span.OutputPreview}");

        foreach (var child in Children(tracer, span))
            PrintSpan(tracer, child, depth + 1);
    }

    private static void PrintLatencyRow(Tracer tracer, TraceSpan span, long origin, long rootDuration, int depth)
    {
        var pad = new string(' ', depth * 2);
        var name = pad + span.Name;
        var share = span.DurationMs / (double)rootDuration;
        Console.WriteLine(
            $"{name,-24} {span.StartMs - origin,8} {span.DurationMs + " ms",10} {share.ToString("0%", CultureInfo.InvariantCulture),8}");
        foreach (var child in Children(tracer, span))
            PrintLatencyRow(tracer, child, origin, rootDuration, depth + 1);
    }

    private static IEnumerable<TraceSpan> Children(Tracer tracer, TraceSpan parent) =>
        tracer.Spans.Where(s => s.ParentId == parent.Id).OrderBy(s => s.StartMs);

    private static void PrintPrice(ModelPrice price) =>
        Console.WriteLine($"  {price.Model,-24} in {Money(price.InputPerMillion),12}    out {Money(price.OutputPerMillion),12}");

    // Six decimals so a mini-model call under one cent is still visible.
    public static string Money(decimal value) =>
        value.ToString("$0.000000", CultureInfo.InvariantCulture);
}
