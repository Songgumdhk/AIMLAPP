namespace AIMLAPP.Learning.AiEvaluationDemo.Models;

public sealed record EvalDoc(string Id, string Title, string Content);

// RelevantDocIds are the gold ids: the answer key retrieval metrics are scored against.
public sealed record EvalCase(string Id, string Question, string[] RelevantDocIds, string Note);

// Score is only comparable within one retriever: 0 for naive, BM25 for keyword,
// cosine for vector. The metrics use rank order and ids, never this raw number.
public sealed record RetrievedHit(string DocId, string Title, double Score, string Content);

// Precision, recall, and hit use K as the cutoff. ReciprocalRank is 1 / rank
// of the first gold document, or 0 when none of the top K is gold.
public sealed record RetrievalScore(
    double PrecisionAtK,
    double RecallAtK,
    double Hit,
    double ReciprocalRank,
    IReadOnlyList<string> RetrievedIds);

public sealed class RetrievalReport
{
    public required IReadOnlyList<RetrievalScore> Naive { get; init; }
    public required IReadOnlyList<RetrievalScore> Keyword { get; init; }
    public required IReadOnlyList<RetrievalScore> Vector { get; init; }
    public required IReadOnlyList<IReadOnlyList<RetrievedHit>> VectorHits { get; init; }
}

// Score 0 means the judge reply could not be parsed into a 1-5 mark.
public sealed record RubricMark(int Score, string Reason);

public sealed record RagJudgement(
    RubricMark ContextRelevance,
    RubricMark AnswerRelevance,
    RubricMark Faithfulness);

// Verdict is "supported", "unsupported", "contradicted", or "unlabeled" (see §6).
public sealed record ClaimVerdict(string Text, string Verdict, string Evidence);

// Winner is "A", "B", "tie", or "" when the judge reply could not be parsed.
public sealed record PairwiseResult(string Reason, string Winner);
