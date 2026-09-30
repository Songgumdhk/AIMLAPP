using OpenAI.Chat;
using System.Text.Json;

namespace AIMLAPP.Learning.AdvancedRagDemo.Retrieval;

// Query rewriting (§10).
// Ask the LLM to expand a vague query into N clearer variants.
// Include the original as one of the variants so we don't lose the intent.
// WHY: "How do I fix it?" has no useful keywords and a fuzzy embedding.
// Each rewrite is searched separately, then RRF merges them (§10.1 multi-query),
// so a chunk matching ANY phrasing gets a chance to surface.
public static class QueryRewriter
{
    public static async Task<List<string>> ExpandAsync(ChatClient chatClient, string query, int count = 3)
    {
        // Ask for count - 1 rewrites because the original query fills the last slot.
        // Hinting at "employee handbook" vocabulary nudges rewrites toward words
        // the corpus actually uses, which helps BM25 as well as vectors.
        // Count comes from appsettings.json → Rag:QueryRewriteCount.
        var prompt =
            $"Rewrite this user query into {count - 1} different search queries that " +
            $"mean the same thing but use different words and phrasings. Include " +
            $"specific terms an employee handbook or product documentation might use. " +
            $"Return ONLY a JSON object like {{\"queries\":[\"q1\",\"q2\",...]}}. " +
            $"\n\nOriginal query: {query}";

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage("You are a search query expander. Output ONLY valid JSON."),
            new UserChatMessage(prompt)
        };

        // JSON mode makes the reply machine-parseable (no prose, no markdown fences).
        // It must return an object, which is why the queries are wrapped in {"queries":[...]}.
        var options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
        };

        var completion = await chatClient.CompleteChatAsync(messages, options);
        var json = completion.Value.Content[0].Text;
        var variants = new List<string> { query }; // always keep the original

        try
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var v in doc.RootElement.GetProperty("queries").EnumerateArray())
            {
                var s = v.GetString();
                if (!string.IsNullOrWhiteSpace(s)) variants.Add(s);
            }
        }
        catch
        {
            // Fallback: just use the original query.
            // WHY: rewriting is an optimization. A bad LLM reply should degrade
            // retrieval to plain hybrid search, not crash the pipeline.
        }
        // Distinct drops a rewrite that just repeats the original; Take caps the list
        // in case the LLM returned more than asked (each variant costs an embedding call).
        return [.. variants.Distinct().Take(count)];
    }
}
