using System.Text.RegularExpressions;
using AIMLAPP.Learning.AdvancedRagDemo.Models;

namespace AIMLAPP.Learning.AdvancedRagDemo.Store;

// In production this is Pinecone / Qdrant / Weaviate / pgvector / Redis.
// For learning, a List + LINQ is more instructive because you can see how
// cosine similarity and BM25 actually work.
public class InMemoryVectorStore
{
    private readonly List<Chunk> _chunks = [];
    private double _avgDocLen; // for BM25 length normalization

    public int Count => _chunks.Count;
    public IReadOnlyList<Chunk> All => _chunks;

    public void Add(Chunk chunk)
    {
        _chunks.Add(chunk);
        // BM25 compares each chunk's length to the corpus average ("avgdl" in §6),
        // so the average must be refreshed whenever the corpus changes.
        _avgDocLen = _chunks.Average(c => TokenizeSimple(c.Content).Length);
    }

    // VECTOR SEARCH (§5)
    // Cosine similarity between the query embedding and every chunk embedding.
    // For our small corpus this is fine. For millions of chunks, use ANN
    // (HNSW, IVF) via a real vector DB.
    // WHY this is a brute-force scan: exact search over every chunk is O(N).
    // ANN indexes trade a tiny bit of recall for sub-linear lookups.
    public List<ScoredChunk> VectorSearch(float[] queryVec, int topK)
    {
        return _chunks
            .Select(c => new ScoredChunk(c, CosineSimilarity(queryVec, c.Embedding)))
            .OrderByDescending(s => s.Score)
            .Take(topK)
            .ToList();
    }

    // KEYWORD SEARCH (§6) — simplified BM25.
    // Score = sum over query terms of IDF(term) * saturated_TF(term, chunk)
    // where saturated_TF = tf * (k1 + 1) / (tf + k1 * length_norm).
    // WHY keyword search at all: embeddings blur exact tokens like "SKU-2287",
    // error codes and names. BM25 matches them literally (§2 "lexical gap").
    public List<ScoredChunk> KeywordSearch(string query, int topK)
    {
        var terms = TokenizeSimple(query);
        if (terms.Length == 0) return [];

        // Precompute document frequency for IDF.
        // df = how many chunks contain the term at least once. A term found in
        // almost every chunk ("the", "acme") carries little signal; a rare one does.
        var docFreq = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in terms.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            docFreq[t] = _chunks.Count(c => TokenizeSimple(c.Content)
                .Any(tok => tok.Equals(t, StringComparison.OrdinalIgnoreCase)));
        }

        // k1 controls TF saturation: the 1st occurrence of a term adds a lot,
        //    the 10th adds little. Higher k1 = repeats keep counting longer.
        // b  controls length normalization: 0 = ignore chunk length, 1 = fully
        //    penalize long chunks (they contain more words by chance).
        // Typical values: k1 in 1.2-2.0, b = 0.75 (Lucene/Elasticsearch use 1.2 / 0.75).
        const double k1 = 1.5, b = 0.75;
        return [.. _chunks
            .Select(c =>
            {
                var docTokens = TokenizeSimple(c.Content);
                var docLen = docTokens.Length;
                double score = 0;
                foreach (var term in terms.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    int tf = docTokens.Count(t => t.Equals(term, StringComparison.OrdinalIgnoreCase));
                    if (tf == 0) continue;

                    int df = docFreq[term];
                    // IDF: rare terms (small df) score high, common terms score near 0.
                    // +1 smoothing so IDF never negative
                    double idf = Math.Log(1 + (_chunks.Count - df + 0.5) / (df + 0.5));
                    // norm > 1 for longer-than-average chunks, < 1 for shorter ones.
                    double norm = 1 - b + b * (docLen / _avgDocLen);
                    score += idf * (tf * (k1 + 1)) / (tf + k1 * norm);
                }
                return new ScoredChunk(c, score);
            })
            .Where(s => s.Score > 0)
            .OrderByDescending(s => s.Score)
            .Take(topK)];
    }

    // FILTER (§9)
    // Returns a NEW store containing only chunks matching the predicate.
    // In a real vector DB this maps to a WHERE clause on metadata.
    // WHY a new store: this is a PRE-filter. Searching the smaller store means
    // BM25 statistics (df, avgdl) and top-K are computed only over allowed chunks,
    // so you never end up with fewer than K results the way a post-filter can.
    public InMemoryVectorStore Filter(Predicate<Chunk> predicate)
    {
        var filtered = new InMemoryVectorStore();
        foreach (var c in _chunks.Where(x => predicate(x))) filtered.Add(c);
        return filtered;
    }

    // Cosine similarity measures the ANGLE between two vectors, not their length:
    // 1 = same direction (same meaning), 0 = unrelated. Dividing by both magnitudes
    // is what removes length from the comparison.
    // OpenAI embeddings are unit-normalized, so this is essentially a
    // dot product — but we compute the full formula for safety.
    // The 1e-10 guards against division by zero for an all-zero vector.
    private static double CosineSimilarity(float[] a, float[] b)
    {
        double dot = 0, magA = 0, magB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }
        return dot / (Math.Sqrt(magA) * Math.Sqrt(magB) + 1e-10);
    }

    // Very simple whitespace + punctuation tokenizer. Real BM25 systems use
    // language-specific analyzers (Snowball stemming, stopword removal, etc.).
    // Note: "SKU-2287" becomes ["sku", "2287"], so the ID still matches exactly.
    // Without stemming, "policy" and "policies" count as different terms.
    private static string[] TokenizeSimple(string text) =>
        Regex.Split(text.ToLowerInvariant(), @"[^a-z0-9]+")
             .Where(t => t.Length > 1)
             .ToArray();
}
