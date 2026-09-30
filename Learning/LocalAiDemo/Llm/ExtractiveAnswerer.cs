using AIMLAPP.Learning.LocalAiDemo.Embeddings;

namespace AIMLAPP.Learning.LocalAiDemo.Llm;

// Answers from the nearest local document. No generative model is required.
// When Ollama is running, option 4 can hand that same document to a local LLM.
public static class ExtractiveAnswerer
{
    public static (LocalDoc Doc, double Score) Best(BagEmbedder embedder, string question)
    {
        var query = embedder.Embed(question);
        LocalDoc? best = null;
        var bestScore = double.MinValue;
        foreach (var doc in LocalCorpus.Docs)
        {
            var score = BagEmbedder.Cosine(query, embedder.Embed(doc.Text));
            if (score > bestScore)
            {
                best = doc;
                bestScore = score;
            }
        }

        return (best!, bestScore);
    }

    // The [id] citation lets the user check the source, same idea as Chapter 3's answers.
    public static string Answer(LocalDoc doc) =>
        $"{doc.Text} [{doc.Id}]";
}
