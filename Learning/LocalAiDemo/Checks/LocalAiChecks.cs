using AIMLAPP.Learning.LocalAiDemo.Embeddings;
using AIMLAPP.Learning.LocalAiDemo.Ollama;
using AIMLAPP.Learning.LocalAiDemo.Onnx;

namespace AIMLAPP.Learning.LocalAiDemo.Checks;

// Locks the offline parts of 08-LocalAI.md. Ollama is not required.
// Runs at startup so the numbers printed in the guide can't silently drift from the code.
public static class LocalAiChecks
{
    public static void Verify()
    {
        // CHECK 1: the "model file" maths from §3.
        // W = [[1,0],[0,2]], b = [0.5,-1], x = [3,4]  →  y = [3.5, 7].
        var output = LocalWeightModel.TinyLinear.Predict([3f, 4f]);
        if (Math.Abs(output[0] - 3.5f) > 0.001f || Math.Abs(output[1] - 7f) > 0.001f)
            throw new InvalidOperationException("Local linear model drifted from y = Wx + b in 08-LocalAI.md.");

        // CHECK 2: local retrieval works. The vacation doc shares "paid", "vacation", "days"
        // with the question; the snacks doc shares none, so it must score lower.
        var embedder = new BagEmbedder(LocalCorpus.Docs.Select(doc => doc.Text));
        var query = embedder.Embed("how many paid vacation days");
        var vacation = BagEmbedder.Cosine(query, embedder.Embed(LocalCorpus.Docs[0].Text));
        var snacks = BagEmbedder.Cosine(query, embedder.Embed(LocalCorpus.Docs[3].Text));
        if (vacation <= snacks)
            throw new InvalidOperationException("Local embeddings did not rank the vacation document first.");

        // CHECK 3: privacy guard. IsLoopback is true only for 127.0.0.1 / ::1 / localhost.
        // Any other host would send prompts off this machine, defeating the chapter's point.
        if (!Uri.TryCreate(OllamaClient.DefaultBaseUrl, UriKind.Absolute, out var ollamaUri) || !ollamaUri.IsLoopback)
            throw new InvalidOperationException("Ollama:BaseUrl in appsettings.json is not pointed at the local machine.");
    }
}
