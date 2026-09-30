using System.Text.RegularExpressions;

namespace AIMLAPP.Learning.LocalAiDemo.Embeddings;

// A local embedding model: text in, a vector out, no network.
// A production local embedder (ONNX MiniLM, or Ollama nomic-embed-text) has the same job.
// This one counts words from a fixed vocabulary so the demo can run with nothing installed.
// Limitation: it only sees SHARED WORDS. "holiday leave" won't match "paid vacation";
// a neural embedder captures meaning and would. See 08-LocalAI.md §4.
public sealed class BagEmbedder
{
    private readonly string[] _vocab;

    // "Training" here is just collecting the vocabulary: one dimension per distinct word.
    // Sorting makes dimension i mean the same word on every run, so vectors are stable.
    // Words outside this vocabulary (e.g. in a question) are simply ignored.
    public BagEmbedder(IEnumerable<string> documents)
    {
        _vocab = documents
            .SelectMany(Tokenize)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(token => token, StringComparer.Ordinal)
            .ToArray();
    }

    public int Dimensions => _vocab.Length;

    public float[] Embed(string text)
    {
        var vector = new float[_vocab.Length];
        foreach (var token in Tokenize(text))
        {
            var index = Array.IndexOf(_vocab, token);
            if (index >= 0)
                vector[index] += 1f;
        }

        // WHY normalize to length 1: a long document has bigger counts just because it has
        // more words. Unit length keeps only the word MIX, so cosine becomes a plain dot product.
        return Normalize(vector);
    }

    // Cosine similarity: the angle between two vectors. 1 = same direction, 0 = nothing shared.
    // Same maths as Chapter 3's vector search; it doesn't care which model made the vectors,
    // only that BOTH vectors came from the same model.
    public static double Cosine(float[] a, float[] b)
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

    public static string[] Tokenize(string text) =>
        Regex.Split(text.ToLowerInvariant(), @"[^a-z0-9]+")
            .Where(token => token.Length > 1)
            .ToArray();

    private static float[] Normalize(float[] vector)
    {
        double mag = 0;
        foreach (var value in vector)
            mag += value * value;
        mag = Math.Sqrt(mag);
        if (mag < 1e-10)
            return vector;

        var unit = new float[vector.Length];
        for (var i = 0; i < vector.Length; i++)
            unit[i] = (float)(vector[i] / mag);
        return unit;
    }
}
