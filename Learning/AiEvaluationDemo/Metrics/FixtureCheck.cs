using AIMLAPP.Learning.AiEvaluationDemo.Data;
using AIMLAPP.Learning.AiEvaluationDemo.Models;
using AIMLAPP.Learning.AiEvaluationDemo.Retrieval;

namespace AIMLAPP.Learning.AiEvaluationDemo.Metrics;

// The keyword rows in 05-AIEvaluation.md are claims about this corpus, not
// about the embedding model. They are checked with no API call.
// WHY: the chapter text and the corpus can drift apart. If someone edits a document and
// a lesson fact stops being true, the demo throws at startup and names the row, instead
// of teaching something false. That is the eval set defending itself (§2, Exercise 6).
// The vector column is deliberately NOT locked: it is the experiment.
public static class FixtureCheck
{
    public static void Verify()
    {
        RetrievalMetrics.VerifyWorkedExample();
        if (RetrievalMetrics.K != 3) return;

        // Fact 1: the naive baseline must never touch a gold document, or it stops being
        // a floor. That is why LabeledSet keeps three distractors in the first slots.
        var index = CorpusIndex.CreateLexical(LabeledSet.Documents);
        var naiveIds = index.NaiveSearch(3).Select(h => h.DocId).ToHashSet(StringComparer.Ordinal);

        foreach (var evalCase in LabeledSet.Cases)
        {
            if (evalCase.RelevantDocIds.Any(naiveIds.Contains))
            {
                throw new InvalidOperationException(
                    $"Naive top 3 overlaps a gold document for {evalCase.Id}. " +
                    "Keep distractors in the first 3 corpus slots.");
            }
        }

        // Fact 2: paraphrase rows share no keyword with their gold doc (keyword must miss).
        // Fact 3: rows with a unique token (9911, EXP-75) put that doc at keyword rank 1.
        // Fact 4: the two-gold row finds both docs in the keyword top K.
        AssertMiss(index, "parental-paraphrase", "hr-parental");
        AssertMiss(index, "locked-out", "it-password");
        AssertFirst(index, "hotline-number", "sec-incident");
        AssertFirst(index, "expense-code", "fin-expense");
        AssertContains(index, "time-off-and-remote", "hr-vacation", "hr-remote");
    }

    private static void AssertMiss(CorpusIndex index, string caseId, string docId)
    {
        var hits = Search(index, caseId);
        if (hits.Any(h => h.DocId == docId))
        {
            throw new InvalidOperationException(
                $"Keyword search was not supposed to return {docId} for {caseId}. " +
                $"Got [{Ids(hits)}]. The lesson in 05-AIEvaluation.md says this question shares no keyword with that document.");
        }
    }

    private static void AssertFirst(CorpusIndex index, string caseId, string docId)
    {
        var hits = Search(index, caseId);
        if (hits.Count == 0 || hits[0].DocId != docId)
        {
            throw new InvalidOperationException(
                $"Expected keyword rank 1 to be {docId} for {caseId}. Got [{Ids(hits)}].");
        }
    }

    private static void AssertContains(CorpusIndex index, string caseId, params string[] docIds)
    {
        var hits = Search(index, caseId);
        var missing = docIds.Where(id => hits.All(h => h.DocId != id)).ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Keyword top {RetrievalMetrics.K} for {caseId} is missing {string.Join(", ", missing)}. Got [{Ids(hits)}].");
        }
    }

    private static List<RetrievedHit> Search(CorpusIndex index, string caseId)
    {
        var evalCase = LabeledSet.Cases.Single(c => c.Id == caseId);
        return index.KeywordSearch(evalCase.Question, RetrievalMetrics.K);
    }

    private static string Ids(IReadOnlyList<RetrievedHit> hits) =>
        hits.Count == 0 ? "none" : string.Join(", ", hits.Select(h => h.DocId));
}
