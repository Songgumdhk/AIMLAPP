using System.Text.Json;
using OpenAI.Chat;

namespace AIMLAPP.Learning.AgenticWorkflowsDemo.Agents;

// One place that talks to the model. Agents stay focused on their role.
// Each agent call is stateless: one system prompt (the role) plus one user message built
// from WorkflowState. No chat history is carried between agents; the state is the handoff.
public static class ChatHelper
{
    public static async Task<string> CompleteAsync(
        ChatClient client,
        string system,
        string user,
        bool json = false)
    {
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(system),
            new UserChatMessage(user),
        };

        // JSON mode makes the model return a valid JSON object. It does not enforce your
        // schema, so callers still validate fields (see PlannerAgent.Parse).
        var options = new ChatCompletionOptions();
        if (json)
            options.ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat();

        var completion = await client.CompleteChatAsync(messages, options);
        return completion.Value.Content.Count > 0
            ? completion.Value.Content[0].Text ?? ""
            : "";
    }

    // Without JSON mode, models often wrap JSON in prose or ``` fences. Keep only the object.
    public static string UnwrapJson(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start >= 0 && end > start)
            return text[start..(end + 1)];
        return text;
    }

    // null instead of an exception, so each agent can choose its own safe fallback.
    public static JsonDocument? ParseOrNull(string text)
    {
        try
        {
            return JsonDocument.Parse(UnwrapJson(text));
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
