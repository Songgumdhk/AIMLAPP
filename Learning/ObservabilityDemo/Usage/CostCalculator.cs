using AIMLAPP.Learning.ObservabilityDemo.Tracing;

namespace AIMLAPP.Learning.ObservabilityDemo.Usage;

// Turns a trace's stored token counts into money using PriceTable.
// Works on any trace after the fact, so the same tokens can be repriced
// when rates change or as a different model. See 06-Observability.md §5-6.
public static class CostCalculator
{
    public static decimal ForSpan(TraceSpan span)
    {
        // retrieve, tool, and request have no model, so they add nothing.
        if (!span.IsModelCall || span.Model is null) return 0m;
        return PriceTable.For(span.Model).Cost(span.InputTokens, span.OutputTokens);
    }

    // Parents carry no model, so summing every span bills each API call once.
    public static decimal ForTrace(IEnumerable<TraceSpan> spans) =>
        spans.Sum(ForSpan);

    // Reprice chat spans as another model. Embedding spans stay on the embedding rate.
    // WHY: answers "what would this exact request cost on gpt-4o?" with pure
    // arithmetic. No second API call. Only possible because tokens were stored.
    public static decimal ChatAs(IEnumerable<TraceSpan> spans, ModelPrice chatPrice)
    {
        decimal total = 0m;
        foreach (var span in spans)
        {
            if (!span.IsModelCall || span.Model is null) continue;
            if (span.Kind == "embed")
                total += PriceTable.Embedding.Cost(span.InputTokens, 0);
            else
                total += chatPrice.Cost(span.InputTokens, span.OutputTokens);
        }

        return total;
    }

    // Locks the arithmetic in 06-Observability.md. Uses the documented rates,
    // not appsettings.json, so editing prices there does not break this check.
    public static void VerifyWorkedExample()
    {
        var gpt4o = new ModelPrice("gpt-4o", 2.50m, 10.00m).Cost(1000, 500);
        var mini = new ModelPrice("gpt-4o-mini", 0.15m, 0.60m).Cost(1000, 500);
        var embed = new ModelPrice("text-embedding-3-small", 0.02m, 0m).Cost(1000, 0);

        if (gpt4o != 0.0075m || mini != 0.00045m || embed != 0.00002m)
        {
            throw new InvalidOperationException(
                "Cost arithmetic drifted from the worked example in 06-Observability.md.");
        }
    }
}
