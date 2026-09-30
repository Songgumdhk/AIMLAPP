using OpenAI.Chat;
using OpenAI.Embeddings;
using AIMLAPP.Configuration;
using AIMLAPP.Learning.AdvancedRagDemo.ConsoleUi;
using AIMLAPP.Learning.AdvancedRagDemo.Embeddings;
using AIMLAPP.Learning.AdvancedRagDemo.Models;
using AIMLAPP.Learning.AdvancedRagDemo.Store;

namespace AIMLAPP.Learning.AdvancedRagDemo.Retrieval;

// Dispatches the 8 retrieval modes from the interactive menu.
// Read these cases top-to-bottom to see how the techniques compose.
// Each case adds one stage from the §3 pipeline diagram on top of the previous one.
public static class RagRetriever
{
    public static async Task<List<ScoredChunk>> RetrieveAsync(
        string? mode,
        string query,
        InMemoryVectorStore store,
        ChatClient chatClient,
        EmbeddingClient embeddingClient)
    {
        // All three knobs live in appsettings.json → Rag section. Try changing them.
        //   TopK             = chunks kept for the final answer ("top 5" in §3)
        //   RerankCandidates = pool size handed to the reranker ("top 20" in §3)
        //   RrfK             = RRF smoothing constant (§7)
        var rag = AppSettings.Current.Rag;
        int topK = rag.TopK;
        int candidates = rag.RerankCandidates;
        int rrfK = rag.RrfK;

        switch (mode)
        {
            case "1":
                {
                    // §5 Vector only: embed the query, cosine similarity, top-K.
                    var qVec = await EmbeddingHelper.EmbedOneAsync(embeddingClient, query);
                    return store.VectorSearch(qVec, topK);
                }
            case "2":
                {
                    // §6 Keyword only. No embedding needed — pure text scoring.
                    return store.KeywordSearch(query, topK);
                }
            case "3":
                {
                    // §7 Hybrid with RRF.
                    // WHY topK * 2 per retriever: a chunk ranked 7th by vectors but 1st by
                    // BM25 must be in both lists for RRF to reward it. Fetch wider, then trim.
                    var qVec = await EmbeddingHelper.EmbedOneAsync(embeddingClient, query);
                    var vecHits = store.VectorSearch(qVec, topK * 2);
                    var kwHits = store.KeywordSearch(query, topK * 2);
                    return Rrf.Merge(vecHits, kwHits, k: rrfK).Take(topK).ToList();
                }
            case "4":
                {
                    // §7 Hybrid + §8 LLM rerank.
                    // Retrieve a wide candidate pool from hybrid, then let the LLM
                    // reorder them by real relevance and keep top-K.
                    var qVec = await EmbeddingHelper.EmbedOneAsync(embeddingClient, query);
                    var vecHits = store.VectorSearch(qVec, candidates);
                    var kwHits = store.KeywordSearch(query, candidates);
                    var merged = Rrf.Merge(vecHits, kwHits, k: rrfK).Take(candidates).ToList();

                    Console.WriteLine($"\n[Before rerank — top {topK} from hybrid RRF]");
                    ResultPrinter.Print(merged.Take(topK).ToList(), showScore: true);

                    // Compare this output with the "Before rerank" list (Exercise 3).
                    var reranked = await Reranker.RerankAsync(chatClient, query, merged, topK);
                    Console.WriteLine($"[After rerank — top {topK}]");
                    return reranked;
                }
            case "5":
                {
                    // §9 Metadata pre-filter + hybrid.
                    // PRE-filter: narrow the store BEFORE searching, so every top-K slot
                    // goes to an allowed chunk. Chunks tagged "All" (company-wide policies)
                    // are kept for every department. In multi-tenant apps this same step
                    // is a security boundary (tenantId), not just a relevance boost.
                    Console.Write("Filter by department? (Engineering / HR / IT / Sales / All): ");
                    var dept = Console.ReadLine()?.Trim();
                    var candidatePool = string.IsNullOrWhiteSpace(dept) || dept.Equals("All", StringComparison.OrdinalIgnoreCase)
                        ? store
                        : store.Filter(c => c.Department.Equals(dept, StringComparison.OrdinalIgnoreCase)
                                         || c.Department == "All");
                    Console.WriteLine($"[Filter applied — {candidatePool.Count} chunks remain]");

                    var qVec = await EmbeddingHelper.EmbedOneAsync(embeddingClient, query);
                    var vecHits = candidatePool.VectorSearch(qVec, topK * 2);
                    var kwHits = candidatePool.KeywordSearch(query, topK * 2);
                    return Rrf.Merge(vecHits, kwHits, k: rrfK).Take(topK).ToList();
                }
            case "6":
                {
                    // §10 Query rewriting: expand vague queries into clearer
                    // ones, then run hybrid on each variant and RRF-merge them all.
                    var variants = await QueryRewriter.ExpandAsync(chatClient, query, count: rag.QueryRewriteCount);
                    Console.WriteLine("[Rewritten queries]");
                    foreach (var v in variants) Console.WriteLine($"  - {v}");

                    // Two levels of RRF: first vector + keyword per variant, then across
                    // variants. A chunk that several phrasings agree on rises to the top.
                    var allHits = new List<List<ScoredChunk>>();
                    foreach (var variant in variants)
                    {
                        var qVec = await EmbeddingHelper.EmbedOneAsync(embeddingClient, variant);
                        var vecHits = store.VectorSearch(qVec, topK);
                        var kwHits = store.KeywordSearch(variant, topK);
                        allHits.Add(Rrf.Merge(vecHits, kwHits, k: rrfK).Take(topK).ToList());
                    }
                    return Rrf.MergeMany(allHits, k: rrfK).Take(topK).ToList();
                }
            case "7":
            case "8":
                {
                    // §11 Full pipeline: rewrite → hybrid → rerank.
                    var variants = await QueryRewriter.ExpandAsync(chatClient, query, count: rag.QueryRewriteCount);
                    Console.WriteLine("[Rewritten queries]");
                    foreach (var v in variants) Console.WriteLine($"  - {v}");

                    var allHits = new List<List<ScoredChunk>>();
                    foreach (var variant in variants)
                    {
                        var qVec = await EmbeddingHelper.EmbedOneAsync(embeddingClient, variant);
                        var vecHits = store.VectorSearch(qVec, topK * 2);
                        var kwHits = store.KeywordSearch(variant, topK * 2);
                        allHits.Add(Rrf.Merge(vecHits, kwHits, k: rrfK).Take(candidates).ToList());
                    }
                    var merged = Rrf.MergeMany(allHits, k: rrfK).Take(candidates).ToList();

                    Console.WriteLine($"\n[Before rerank — top {topK} from full hybrid]");
                    ResultPrinter.Print(merged.Take(topK).ToList(), showScore: true);

                    // WHY rerank against the ORIGINAL query, not the rewrites: rewrites only
                    // widen recall. Final relevance is judged by what the user actually asked.
                    var reranked = await Reranker.RerankAsync(chatClient, query, merged, topK);
                    Console.WriteLine($"[After rerank — top {topK}]");
                    return reranked;
                }
            default:
                Console.WriteLine("Unknown mode.");
                return new List<ScoredChunk>();
        }
    }
}
