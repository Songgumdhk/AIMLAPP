using OpenAI.Embeddings;
using AIMLAPP.Learning.AiEvaluationDemo.Data;
using AIMLAPP.Learning.AiEvaluationDemo.Models;
using AIMLAPP.Learning.AiEvaluationDemo.Retrieval;

namespace AIMLAPP.Learning.AiEvaluationDemo.Metrics;

// THE RETRIEVAL HARNESS — runs every retriever on every labeled question and scores
// each result against the gold ids. Same questions, same K, same metrics for all three,
// so the only thing that differs between columns is the retriever (the experiment).
// See 05-AIEvaluation.md §2 and §8.
public static class RetrievalHarness
{
    public static async Task<RetrievalReport> RunAsync(CorpusIndex index, EmbeddingClient embeddings)
    {
        var cases = LabeledSet.Cases;

        // STEP 1: Embed all six questions in one request instead of six round trips.
        var queryVectors = await CorpusIndex.EmbedQueriesAsync(
            embeddings,
            cases.Select(c => c.Question).ToList());

        var naive = new List<RetrievalScore>();
        var keyword = new List<RetrievalScore>();
        var vector = new List<RetrievalScore>();
        var vectorHits = new List<IReadOnlyList<RetrievedHit>>();

        // STEP 2: For each question, retrieve with all three, then score against gold.
        for (var i = 0; i < cases.Count; i++)
        {
            var gold = cases[i].RelevantDocIds;
            var naiveHits = index.NaiveSearch(RetrievalMetrics.K);
            var keywordHits = index.KeywordSearch(cases[i].Question, RetrievalMetrics.K);
            var denseHits = index.VectorSearch(queryVectors[i], RetrievalMetrics.K);

            naive.Add(RetrievalMetrics.Score(Ids(naiveHits), gold));
            keyword.Add(RetrievalMetrics.Score(Ids(keywordHits), gold));
            vector.Add(RetrievalMetrics.Score(Ids(denseHits), gold));

            // Keep the vector hits: option 5 generates and grades answers from exactly the
            // documents it just scored, so Hit and Faith in the grade table line up.
            vectorHits.Add(denseHits);
        }

        return new RetrievalReport
        {
            Naive = naive,
            Keyword = keyword,
            Vector = vector,
            VectorHits = vectorHits,
        };
    }

    private static List<string> Ids(IReadOnlyList<RetrievedHit> hits) =>
        hits.Select(h => h.DocId).ToList();
}
