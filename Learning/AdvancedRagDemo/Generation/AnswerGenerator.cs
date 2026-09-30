using System.Text;
using OpenAI.Chat;
using AIMLAPP.Learning.AdvancedRagDemo.Models;

namespace AIMLAPP.Learning.AdvancedRagDemo.Generation;

// The "G" in RAG: stuff retrieved chunks into the prompt as grounded
// context. Instruct the LLM to answer ONLY from that context and cite.
// The model's weights don't change; the chunks are its "cheat sheet" at question time (§1).
public static class AnswerGenerator
{
    public static async Task<string> GenerateAsync(
        ChatClient chatClient,
        string query,
        List<ScoredChunk> retrieved)
    {
        // STEP 1: Build a numbered CONTEXT block.
        // WHY numbers + chunk ids: the model can cite [1], [2] and a reader can map each
        // citation back to a real chunk id to verify the claim (§12 "Not showing citations").
        // Department metadata is included so the model can tell policies apart.
        var sb = new StringBuilder();
        for (int i = 0; i < retrieved.Count; i++)
        {
            sb.AppendLine($"[{i + 1}] (id={retrieved[i].Chunk.Id}, dept={retrieved[i].Chunk.Department})");
            sb.AppendLine(retrieved[i].Chunk.Content);
            sb.AppendLine();
        }

        // STEP 2: Grounding rules go in the system message, data in the user message.
        // CRITICAL: the explicit "I don't know" escape hatch reduces hallucination.
        // Without it, the model tends to fill gaps from its training data.
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(
                "You are a helpful assistant. Answer the user's question STRICTLY " +
                "using the provided context chunks. If the answer is not in the " +
                "context, say 'I don't know based on the provided documents.' " +
                "Always cite the chunk numbers you used, like [1] or [2, 3]."),
            new UserChatMessage($"CONTEXT:\n{sb}\n\nQUESTION: {query}")
        };

        var completion = await chatClient.CompleteChatAsync(messages);
        return completion.Value.Content[0].Text;
    }
}
