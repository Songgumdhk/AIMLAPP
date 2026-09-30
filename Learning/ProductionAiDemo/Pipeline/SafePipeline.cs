using AIMLAPP.Learning.ProductionAiDemo.Caching;
using AIMLAPP.Learning.ProductionAiDemo.Guardrails;
using AIMLAPP.Learning.ProductionAiDemo.RateLimit;
using AIMLAPP.Learning.ProductionAiDemo.Security;

namespace AIMLAPP.Learning.ProductionAiDemo.Pipeline;

public sealed record StepNote(string Step, string Result);

// ModelCalled is the key field: every early exit keeps it false, which is the proof
// that a blocked, limited, or cached request cost no tokens.
public sealed class PipelineResult
{
    public required string Status { get; init; }
    public required string Answer { get; init; }
    public required IReadOnlyList<StepNote> Steps { get; init; }
    public required bool ModelCalled { get; init; }
}

// The order is the deployment story:
// rate limit → input guard → cache → quarantine docs → model → output guard → store.
// Cheap checks run first, so most bad requests stop before they cost anything.
// See 07-ProductionAI.md §6-7.
public static class SafePipeline
{
    public static async Task<PipelineResult> RunAsync(
        string question,
        AnswerCache cache,
        WindowLimiter limiter,
        DateTimeOffset now,
        TimeSpan ttl,
        Func<string, Task<string>> complete,
        bool fromModel)
    {
        var steps = new List<StepNote>();

        // STEP 1: Rate limit. First, so a runaway loop or abusive caller is
        // rejected before any other work. A rejected request costs nothing.
        if (!limiter.TryAcquire(now))
        {
            steps.Add(new StepNote("rate limit", "rejected"));
            return Finish("limited", "", steps, modelCalled: false);
        }

        steps.Add(new StepNote("rate limit", $"allowed, remaining {limiter.Remaining(now)}"));

        // STEP 2: Input guard, before the cache. Its rules apply to every request,
        // even one the cache could answer. A blocked question never reaches the model.
        var input = InputGuard.Check(question);
        steps.Add(new StepNote("input guard", input.Allowed ? "ok" : input.Rule + ": " + input.Detail));
        if (!input.Allowed)
            return Finish("blocked", "", steps, modelCalled: false);

        // STEP 3: Cache lookup. Anything in the cache already passed the output guard,
        // so a hit can be returned without calling the model or re-checking it.
        if (cache.TryGet(question, now, out var cached))
        {
            steps.Add(new StepNote("cache", "hit"));
            return Finish("cached", cached, steps, modelCalled: false);
        }

        steps.Add(new StepNote("cache", "miss"));

        // STEP 4: Drop poisoned documents (indirect injection) before the prompt is built.
        // Once text is in the prompt, the model may follow it. Your code decides first.
        var (kept, quarantined) = DocScanner.Split(PolicyDocs.Retrieved);
        steps.Add(new StepNote(
            "documents",
            $"kept {string.Join(", ", kept.Select(d => d.Id))}; quarantined {ListIds(quarantined)}"));

        // STEP 5: The model. `complete` sees only the kept documents.
        // The poisoned note never enters the prompt. Each doc is tagged [id]
        // so the model can cite it, and the output guard can check the citation.
        var handbook = string.Join("\n", kept.Select(d => $"[{d.Id}] {d.Body}"));
        var raw = await complete(handbook);
        steps.Add(new StepNote("answer", fromModel ? "model called" : "canned"));

        // STEP 6: Output guard. The model's text is untrusted until checked.
        var output = OutputGuard.Check(raw);
        steps.Add(new StepNote("output guard", output.Allowed ? output.Rule : output.Rule + ": " + output.Detail));
        // CRITICAL: return before the cache store. A blocked answer is never cached,
        // or the next identical question would be served the rejected text.
        if (!output.Allowed)
            return Finish("blocked-output", raw, steps, fromModel);

        // STEP 7: Cache store, last. Only answers that passed every check are saved.
        var stored = cache.Set(question, raw, now, ttl);
        steps.Add(new StepNote("cache store", stored ? "saved" : "skipped"));
        return Finish("answered", raw, steps, fromModel);
    }

    // Stand-in for the model so the path runs without an API call. It still goes
    // through the output guard, so it must cite [hr-vacation] like a real answer.
    public static string CannedAnswer(string handbook)
    {
        if (handbook.Contains("hr-vacation", StringComparison.Ordinal))
            return "New hires accrue 15 days of paid vacation per year. [hr-vacation]";

        return "I don't know based on the handbook.";
    }

    private static PipelineResult Finish(string status, string answer, List<StepNote> steps, bool modelCalled) =>
        new()
        {
            Status = status,
            Answer = answer,
            Steps = steps,
            ModelCalled = modelCalled,
        };

    private static string ListIds(IReadOnlyList<PolicyDoc> docs) =>
        docs.Count == 0 ? "(none)" : string.Join(", ", docs.Select(d => d.Id));
}
