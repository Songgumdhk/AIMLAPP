using System.Text.RegularExpressions;
using OpenAI.Embeddings;
using AIMLAPP.Learning.AiEvaluationDemo.Models;

namespace AIMLAPP.Learning.AiEvaluationDemo.Retrieval;

// THE THREE RETRIEVERS UNDER TEST — naive (the floor), keyword (BM25), vector (cosine).
// One document is one retrieval unit, so gold ids line up with hits.
// Keyword scoring is the same BM25 idea as chapter 3, scaled down for this set.
// See 05-AIEvaluation.md §4.
public sealed class CorpusIndex
{
    // Besides grammar words, this drops question filler such as "someone" and "person".
    // Without it, "person" in a question would match "per person" in the EXP-75 document
    // by accident. FixtureCheck's keyword facts depend on this exact list.
    private static readonly HashSet<string> Stopwords = new(StringComparer.Ordinal)
    {
        "a", "an", "the", "of", "to", "for", "and", "or", "in", "on", "is", "are",
        "how", "do", "does", "did", "i", "my", "me", "what", "who", "can", "with",
        "their", "from", "after", "someone", "person", "be", "it", "at", "by",
        "this", "that", "your", "you", "many", "am", "into", "not",
    };

    private readonly List<IndexedDoc> _docs;

    private CorpusIndex(List<IndexedDoc> docs) => _docs = docs;

    // Keyword-only index with no embeddings, so no API call. FixtureCheck uses it at
    // startup to verify the keyword facts in the chapter for free.
    public static CorpusIndex CreateLexical(IReadOnlyList<EvalDoc> docs)
    {
        var indexed = docs
            .Select(d => new IndexedDoc(d, [], Tokenize(d.Title + "\n" + d.Content)))
            .ToList();
        return new CorpusIndex(indexed);
    }

    public static async Task<CorpusIndex> BuildAsync(EmbeddingClient client, IReadOnlyList<EvalDoc> docs)
    {
        // STEP 1: Embed all documents in one batched request (OpenAI:EmbeddingModel).
        // Title + content, the same text the keyword side tokenizes, so both retrievers
        // see identical input and the comparison is fair.
        var inputs = docs.Select(d => d.Title + "\n" + d.Content).ToList();
        var response = await client.GenerateEmbeddingsAsync(inputs);

        // STEP 2: Sort by Index so vector i belongs to document i. A mismatch here would
        // silently attach one document's meaning to another document's id.
        var vectors = response.Value
            .OrderBy(e => e.Index)
            .Select(e => e.ToFloats().ToArray())
            .ToList();

        if (vectors.Count != docs.Count)
            throw new InvalidOperationException("Embedding count did not match the corpus.");

        var indexed = new List<IndexedDoc>();
        for (var i = 0; i < docs.Count; i++)
            indexed.Add(new IndexedDoc(docs[i], vectors[i], Tokenize(inputs[i])));

        return new CorpusIndex(indexed);
    }

    // Questions must be embedded with the same model as the documents, or the vectors
    // live in different spaces and cosine similarity means nothing.
    public static async Task<List<float[]>> EmbedQueriesAsync(EmbeddingClient client, IReadOnlyList<string> queries)
    {
        var response = await client.GenerateEmbeddingsAsync(queries);
        return response.Value
            .OrderBy(e => e.Index)
            .Select(e => e.ToFloats().ToArray())
            .ToList();
    }

    // Ignores the question. This is the floor the other retrievers have to beat.
    // WHY keep a baseline this dumb: a score only means something next to a reference.
    // If a "smart" retriever cannot beat "first k documents", the metric or the set is
    // broken. LabeledSet puts distractors first, so naive hit rate here is 0.
    public List<RetrievedHit> NaiveSearch(int k) =>
        _docs.Take(k)
            .Select(d => new RetrievedHit(d.Doc.Id, d.Doc.Title, 0, d.Doc.Content))
            .ToList();

    public List<RetrievedHit> KeywordSearch(string query, int k)
    {
        var terms = Tokenize(query).Distinct(StringComparer.Ordinal).ToArray();
        if (terms.Length == 0) return [];

        // BM25 knobs, set to the textbook defaults:
        //   k1 = term-frequency saturation. The 2nd match of a word adds less than the 1st,
        //        so repeating a word cannot dominate the score.
        //   b  = length normalization. 0 ignores document length, 1 fully penalizes long
        //        documents that match simply because they contain more words.
        const double k1 = 1.5;
        const double b = 0.75;
        var avg = _docs.Average(d => d.Tokens.Length);
        if (avg <= 0) avg = 1;

        var scored = new List<RetrievedHit>();
        foreach (var doc in _docs)
        {
            double score = 0;
            foreach (var term in terms)
            {
                var tf = 0;
                foreach (var token in doc.Tokens)
                    if (token == term) tf++;
                if (tf == 0) continue;

                var df = 0;
                foreach (var other in _docs)
                    if (other.Tokens.Contains(term, StringComparer.Ordinal)) df++;

                // IDF: a word found in one document (like "9911" or "exp") is strong
                // evidence. A word found in many documents is weak evidence.
                var idf = Math.Log(1 + (_docs.Count - df + 0.5) / (df + 0.5));
                var norm = 1 - b + b * (doc.Tokens.Length / avg);
                score += idf * (tf * (k1 + 1)) / (tf + k1 * norm);
            }

            // No shared token means no hit at all. Keyword search can return fewer than k
            // documents, and Precision@K still divides by K, so empty slots are misses.
            if (score > 0)
                scored.Add(new RetrievedHit(doc.Doc.Id, doc.Doc.Title, score, doc.Doc.Content));
        }

        return scored.OrderByDescending(h => h.Score).Take(k).ToList();
    }

    public List<RetrievedHit> VectorSearch(float[] query, int k)
    {
        if (_docs.Any(d => d.Vector.Length == 0))
            throw new InvalidOperationException("Build the embedding index before vector search.");

        return _docs
            .Select(d => new RetrievedHit(d.Doc.Id, d.Doc.Title, Cosine(query, d.Vector), d.Doc.Content))
            .OrderByDescending(h => h.Score)
            .Take(k)
            .ToList();
    }

    // Cosine similarity compares the direction of two embeddings, not their length.
    // Close to 1 = similar meaning, even with no shared words ("baby" vs "child").
    // The 1e-10 guards against dividing by zero on an all-zero vector.
    private static double Cosine(float[] a, float[] b)
    {
        double dot = 0, magA = 0, magB = 0;
        var n = Math.Min(a.Length, b.Length);
        for (var i = 0; i < n; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        return dot / (Math.Sqrt(magA) * Math.Sqrt(magB) + 1e-10);
    }

    // Lowercase, split on anything that is not a letter or digit, drop 1-char tokens and
    // stopwords. "EXP-75" becomes "exp" and "75". No stemming, so "paid" and "pay" stay
    // different tokens: that is exactly the gap parental-paraphrase is built to expose.
    internal static string[] Tokenize(string text) =>
        Regex.Split(text.ToLowerInvariant(), @"[^a-z0-9]+")
            .Where(t => t.Length > 1 && !Stopwords.Contains(t))
            .ToArray();

    private sealed record IndexedDoc(EvalDoc Doc, float[] Vector, string[] Tokens);
}
