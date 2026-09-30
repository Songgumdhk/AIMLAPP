using AIMLAPP.Learning.AiEvaluationDemo.Models;

namespace AIMLAPP.Learning.AiEvaluationDemo.Metrics;

// THE RETRIEVAL FORMULAS — the only place precision, recall, hit, and reciprocal rank
// are computed. They compare retrieved ids to gold ids, so no model is involved and
// no model can argue with the result. See 05-AIEvaluation.md §4.
public static class RetrievalMetrics
{
    // The cutoff: only the top K results count. It stays in code, not appsettings.json,
    // on purpose: the frozen labeled set, the worked example, and FixtureCheck are all
    // written for K = 3. Change it and those checks skip themselves.
    public static readonly int K = 3;

    public static RetrievalScore Score(IReadOnlyList<string> retrievedIds, IReadOnlyList<string> relevantIds)
    {
        // STEP 1: Keep the first K distinct ids. A duplicate must not count as a second hit.
        var gold = relevantIds.ToHashSet(StringComparer.Ordinal);
        var top = new List<string>();
        foreach (var id in retrievedIds)
        {
            if (top.Count == K) break;
            if (top.Contains(id, StringComparer.Ordinal)) continue;
            top.Add(id);
        }

        // STEP 2: The four numbers. Each answers a different question:
        //   Precision@K - how much of the top K is gold? (how much junk did we show?)
        //   Recall@K    - how many of the gold docs did we find? (did we miss one?)
        //   Hit@K       - did at least one gold doc show up? (the simplest pass/fail)
        //   RR          - 1 / rank of the first gold doc. Rewards putting it near the top.
        //                 MRR is the mean of RR across all questions.
        var relevantHits = top.Count(gold.Contains);

        // CRITICAL: divide by K, not by how many came back. A retriever that returns one
        // correct doc and nothing else scores 1/3, not 1. Empty slots count as misses.
        var precision = (double)relevantHits / K;
        var recall = gold.Count == 0 ? 0 : (double)relevantHits / gold.Count;
        var hit = relevantHits > 0 ? 1.0 : 0.0;
        var rankIndex = top.FindIndex(id => gold.Contains(id));
        var reciprocalRank = rankIndex < 0 ? 0 : 1.0 / (rankIndex + 1);

        return new RetrievalScore(precision, recall, hit, reciprocalRank, top);
    }

    // Locks the arithmetic in 05-AIEvaluation.md. Skipped if you change K,
    // because that write-up is the K = 3 example.
    public static void VerifyWorkedExample()
    {
        if (K != 3) return;

        // The three worked examples from §4: one of two gold docs found, a one-item list
        // (precision still 1/3), and a total miss (Hit = 0, RR = 0).
        var partial = Score(
            ["hr-vacation", "hr-onboarding", "fin-expense"],
            ["hr-vacation", "hr-remote"]);

        var shortList = Score(["hr-vacation"], ["hr-vacation"]);
        var miss = Score(["fin-expense"], ["hr-vacation"]);

        if (Math.Abs(partial.PrecisionAtK - (1.0 / 3)) > 0.001
            || Math.Abs(partial.RecallAtK - 0.5) > 0.001
            || partial.Hit != 1
            || partial.ReciprocalRank != 1
            || Math.Abs(shortList.PrecisionAtK - (1.0 / 3)) > 0.001
            || shortList.RecallAtK != 1
            || miss.Hit != 0
            || miss.ReciprocalRank != 0)
        {
            throw new InvalidOperationException(
                "RetrievalMetrics drifted from the worked example in 05-AIEvaluation.md.");
        }
    }
}
