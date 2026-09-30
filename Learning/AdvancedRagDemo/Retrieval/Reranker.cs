using System.Text;
using System.Text.Json;
using OpenAI.Chat;
using AIMLAPP.Learning.AdvancedRagDemo.Models;

namespace AIMLAPP.Learning.AdvancedRagDemo.Retrieval;

// LLM-as-cross-encoder (§8).
// Ask the chat model (OpenAI:ChatModel, gpt-4o by default) to score each candidate.
// Slower + more expensive than vector search but MUCH more accurate.
// In production, use Cohere Rerank or a self-hosted bge-reranker instead.
// WHY it beats cosine: embeddings compress query and chunk SEPARATELY. A reranker
// reads the query and the chunk TOGETHER, so it can tell "actually answers this"
// from "just shares vocabulary".
public static class Reranker
{
    // WHY only a candidate pool: reranking cost grows linearly with the number of
    // candidates. Cheap hybrid search narrows to ~20 (Rag:RerankCandidates), then this
    // expensive step picks the best Rag:TopK. Reranking the whole corpus would not scale.
    public static async Task<List<ScoredChunk>> RerankAsync(
        ChatClient chatClient,
        string query,
        List<ScoredChunk> candidates,
        int topK)
    {
        if (candidates.Count == 0) return new List<ScoredChunk>();

        // Keep candidate previews short — the reranker doesn't need the
        // full chunk, just enough to judge relevance.
        // Candidates are labelled by list index [0], [1]... rather than chunk id:
        // short integers are easier for the model to echo back exactly.
        var sb = new StringBuilder();
        for (int i = 0; i < candidates.Count; i++)
        {
            var text = candidates[i].Chunk.Content.Replace("\n", " ");
            if (text.Length > 400) text = text.Substring(0, 400) + "...";
            sb.AppendLine($"[{i}] {text}");
        }

        var prompt =
            $"Rate the relevance of each candidate document to the query on a scale " +
            $"0.0 (irrelevant) to 1.0 (perfect answer). Return ONLY a JSON array like " +
            $"[{{\"id\":0,\"score\":0.9}},{{\"id\":1,\"score\":0.2}}].\n\n" +
            $"Query: {query}\n\nCandidates:\n{sb}";

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage("You are a strict document relevance scorer. Output ONLY valid JSON."),
            new UserChatMessage(prompt)
        };

        var options = new ChatCompletionOptions
        {
            // Force JSON output — no chatter, no markdown fences.
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
        };
        // JSON object mode requires the word "json" in the prompt (which we have)
        // and returns an object, so we wrap the array in one.
        messages[messages.Count - 1] = new UserChatMessage(prompt +
            "\n\nWrap your answer as {\"scores\":[...]}.");

        var completion = await chatClient.CompleteChatAsync(messages, options);
        var json = completion.Value.Content[0].Text;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var arr = doc.RootElement.GetProperty("scores");
            var scored = new List<ScoredChunk>();
            foreach (var entry in arr.EnumerateArray())
            {
                int id = entry.GetProperty("id").GetInt32();
                double score = entry.GetProperty("score").GetDouble();
                // CRITICAL: never trust LLM output blindly. Ignore ids outside the
                // candidate list instead of throwing IndexOutOfRange.
                if (id >= 0 && id < candidates.Count)
                    scored.Add(new ScoredChunk(candidates[id].Chunk, score));
            }
            return scored.OrderByDescending(s => s.Score).Take(topK).ToList();
        }
        catch (Exception ex)
        {
            // Fall back to the pre-rerank order so the pipeline still returns
            // something useful if the LLM didn't produce valid JSON.
            Console.WriteLine($"[Reranker parse error: {ex.Message}. Falling back to input order.]");
            return candidates.Take(topK).ToList();
        }
    }
}
