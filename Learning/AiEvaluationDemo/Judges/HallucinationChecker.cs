using System.Text.Json;
using OpenAI.Chat;
using AIMLAPP.Learning.AiEvaluationDemo.Models;

namespace AIMLAPP.Learning.AiEvaluationDemo.Judges;

// CLAIM-LEVEL HALLUCINATION CHECK — the evidence under the rubric's faithfulness score.
// The model splits an answer into atomic claims and labels each one against CONTEXT.
// Your code, not the model, turns those labels into a rate. See 05-AIEvaluation.md §6.
public static class HallucinationChecker
{
    // WHY claims instead of one verdict for the whole answer: a mostly-true answer can
    // still contain one invented sentence. Labeling claim by claim lets you point at it.
    // "unsupported" (context is silent) and "contradicted" (context says the opposite)
    // are kept apart because they are different failures, though both count as bad.
    // The evidence quote makes each "supported" label checkable by a human.
    public const string SystemPrompt =
        "You check an answer claim by claim. You do not answer the question. " +
        "Split ANSWER into atomic factual claims. " +
        "For each claim, verdict is supported, unsupported, or contradicted. " +
        "supported: CONTEXT states it. " +
        "contradicted: CONTEXT states the opposite. " +
        "unsupported: CONTEXT does not state it. Outside knowledge is not support. " +
        "evidence is a short quote from CONTEXT, or an empty string. " +
        "Reply as JSON: {\"claims\":[{\"text\":\"...\",\"verdict\":\"supported\",\"evidence\":\"...\"}]}";

    public static async Task<IReadOnlyList<ClaimVerdict>> CheckAsync(
        ChatClient client,
        string context,
        string answer)
    {
        var raw = await EvalChat.CompleteAsync(
            client,
            SystemPrompt,
            $"CONTEXT:\n{context}\n\nANSWER:\n{answer}",
            json: true);

        using var document = EvalChat.Parse(raw);

        // An empty list means "the checker failed", and the printer says "no claims parsed".
        // It is not the same as "no hallucinations".
        if (document is null) return [];

        var array = EvalChat.Prop(document.RootElement, "claims");
        if (array is null || array.Value.ValueKind != JsonValueKind.Array)
            return [];

        var verdicts = new List<ClaimVerdict>();
        foreach (var item in array.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var text = EvalChat.ReadString(item, "text");
            if (string.IsNullOrWhiteSpace(text)) continue;

            var verdict = Normalize(EvalChat.ReadString(item, "verdict"));
            var evidence = EvalChat.ReadString(item, "evidence");
            verdicts.Add(new ClaimVerdict(text.Trim(), verdict, evidence.Trim()));
        }

        return verdicts;
    }

    // The rate is arithmetic on the labels. The model does not get to report it.
    // If you let the model report the rate, you have graded the grader's arithmetic.
    //   hallucination rate = (unsupported + contradicted) / labeled claims
    // Unlabeled claims (a verdict we could not map) are counted but left out of the
    // denominator, so a sloppy label cannot quietly make the answer look better.
    public static (int Supported, int Unsupported, int Contradicted, int Unlabeled, double Rate) Summarize(
        IReadOnlyList<ClaimVerdict> claims)
    {
        var supported = claims.Count(c => c.Verdict == "supported");
        var unsupported = claims.Count(c => c.Verdict == "unsupported");
        var contradicted = claims.Count(c => c.Verdict == "contradicted");
        var unlabeled = claims.Count - supported - unsupported - contradicted;
        var labeled = supported + unsupported + contradicted;
        var rate = labeled == 0 ? 0 : (double)(unsupported + contradicted) / labeled;
        return (supported, unsupported, contradicted, unlabeled, rate);
    }

    // Models drift from the exact words in the prompt ("entailed", "not supported",
    // "refuted"). Map common synonyms onto the three verdicts; anything else is unlabeled.
    private static string Normalize(string verdict)
    {
        var value = verdict.Trim().ToLowerInvariant();
        return value switch
        {
            "supported" or "support" or "entailed" => "supported",
            "unsupported" or "not_supported" or "not supported" or "hallucinated" => "unsupported",
            "contradicted" or "contradiction" or "refuted" => "contradicted",
            _ => "unlabeled",
        };
    }
}
