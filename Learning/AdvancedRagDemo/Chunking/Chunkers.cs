using System.Text.RegularExpressions;

namespace AIMLAPP.Learning.AdvancedRagDemo.Chunking;

// Three chunking strategies (§4). In real projects you'd add a fourth
// (semantic chunking) by embedding sentences and splitting on similarity drops.
public static class Chunkers
{
    // Fixed-size with overlap. Easy, deterministic, but cuts mid-sentence.
    // Sizes are in characters, not tokens (Rag:FixedChunkSize / Rag:FixedChunkOverlap).
    public static IEnumerable<string> FixedSize(string text, int maxChars, int overlap)
    {
        text = text.Trim();
        if (text.Length <= maxChars) { yield return text; yield break; }

        // WHY overlap: a fact that straddles a boundary would otherwise be split
        // across two chunks and match neither well. Each window starts `overlap`
        // chars before the previous one ended, so the boundary text appears in both.
        // Cost: duplicated text in the index (more storage, near-duplicate hits).
        int step = maxChars - overlap;
        for (int start = 0; start < text.Length; start += step)
        {
            int len = Math.Min(maxChars, text.Length - start);
            yield return text.Substring(start, len);
            if (start + len >= text.Length) yield break;
        }
    }

    // Sentence-based. Grouped N sentences per chunk. Never cuts mid-sentence.
    // N comes from Rag:SentencesPerChunk. Chunk size varies with sentence length.
    public static IEnumerable<string> BySentence(string text, int sentencesPerChunk)
    {
        // Split AFTER . ! or ? followed by whitespace (the lookbehind keeps the
        // punctuation on the sentence). Naive: abbreviations like "e.g. " fool it.
        var sentences = Regex.Split(text.Trim(), @"(?<=[\.\!\?])\s+")
                             .Where(s => s.Length > 0)
                             .ToList();
        for (int i = 0; i < sentences.Count; i += sentencesPerChunk)
        {
            yield return string.Join(" ", sentences.Skip(i).Take(sentencesPerChunk));
        }
    }

    // Paragraph-based. Split on blank lines (author-defined structure).
    // Usually the best default for human-written docs: one paragraph ≈ one idea.
    // In SampleCorpus the title line becomes its own small chunk.
    public static IEnumerable<string> ByParagraph(string text)
    {
        return Regex.Split(text.Trim(), @"\r?\n\r?\n+")
                    .Select(p => p.Trim())
                    .Where(p => p.Length > 0);
    }
}
