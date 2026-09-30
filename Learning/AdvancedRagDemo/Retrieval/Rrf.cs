using AIMLAPP.Learning.AdvancedRagDemo.Models;

namespace AIMLAPP.Learning.AdvancedRagDemo.Retrieval;

// Reciprocal Rank Fusion (§7)
// Combines multiple ranked lists into one. Uses only RANKS, not raw scores,
// so it works fairly across incompatible scoring systems (cosine vs BM25).
// Cosine lives in [0, 1]; BM25 is unbounded (20+ is common). Averaging them
// would let BM25 dominate. Ranks put both lists on the same scale.
// Used twice in this demo: vector + keyword (hybrid), and across query rewrites.
public static class Rrf
{
    public static List<ScoredChunk> Merge(
        List<ScoredChunk> listA,
        List<ScoredChunk> listB,
        int k = 60) => MergeMany([listA, listB], k);

    public static List<ScoredChunk> MergeMany(
        IEnumerable<List<ScoredChunk>> lists,
        int k = 60)
    {
        // Keyed by chunk id so the same chunk found by several lists is summed, not duplicated.
        var scores = new Dictionary<string, (Chunk chunk, double score)>();

        foreach (var list in lists)
        {
            for (int rank = 0; rank < list.Count; rank++)
            {
                var chunk = list[rank].Chunk;
                // RRF(d) = sum over lists of 1 / (k + rank).
                // WHY k: it flattens the curve so rank 1 vs rank 2 is a small gap.
                // A chunk ranked well in BOTH lists beats one ranked #1 in only one.
                // Smaller k = more weight on the very top ranks.
                // Default 60. Tune in appsettings.json → Rag:RrfK.
                double rrfBoost = 1.0 / (k + rank + 1); // ranks are 1-based
                if (scores.TryGetValue(chunk.Id, out var existing))
                {
                    scores[chunk.Id] = (existing.chunk, existing.score + rrfBoost);
                }
                else
                {
                    scores[chunk.Id] = (chunk, rrfBoost);
                }
            }
        }

        return scores.Values
            .Select(v => new ScoredChunk(v.chunk, v.score))
            .OrderByDescending(s => s.Score)
            .ToList();
    }
}
