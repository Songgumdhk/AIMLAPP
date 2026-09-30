using OpenAI.Embeddings;

namespace AIMLAPP.Learning.AdvancedRagDemo.Embeddings;

// Thin wrapper around the OpenAI embeddings API so callers don't repeat
// ToFloats() conversion. Always use the SAME model for docs and queries.
// WHY: each model maps text into its own vector space. A query vector from one
// model compared to chunk vectors from another gives meaningless cosine scores.
// Model name comes from appsettings.json → OpenAI:EmbeddingModel.
public static class EmbeddingHelper
{
    // Used at query time: one short query → one vector.
    public static async Task<float[]> EmbedOneAsync(EmbeddingClient client, string text)
    {
        var r = await client.GenerateEmbeddingAsync(text);
        return r.Value.ToFloats().ToArray();
    }

    // Used at ingestion: many chunks in ONE request. Results come back in input
    // order, so embeddings[i] belongs to texts[i].
    public static async Task<List<float[]>> EmbedBatchAsync(EmbeddingClient client, IReadOnlyList<string> texts)
    {
        var r = await client.GenerateEmbeddingsAsync(texts);
        return r.Value.Select(e => e.ToFloats().ToArray()).ToList();
    }
}
