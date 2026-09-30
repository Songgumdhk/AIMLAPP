using System.Globalization;
using System.Text.Json;
using OpenAI.Chat;
using AIMLAPP.Configuration;
using AIMLAPP.Learning.AiEvaluationDemo.Models;

namespace AIMLAPP.Learning.AiEvaluationDemo.Judges;

// SHARED PLUMBING FOR EVERY MODEL CALL IN CHAPTER 5 — the answerer and all three judges.
// One place to send a prompt, and one place to read judge JSON defensively.
// A judge is a measurement instrument, so a malformed reply must become a visible
// "no score" (0), never a crash and never a made-up number. See 05-AIEvaluation.md §7-8.
public static class EvalChat
{
    public static async Task<string> CompleteAsync(
        ChatClient client,
        string system,
        string user,
        bool json)
    {
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(system),
            new UserChatMessage(user),
        };

        // WHY temperature 0 (OpenAI:Temperature in appsettings.json): a rerun of the same
        // judge on the same input should move as little as possible. It still can move.
        // A judge is not a pure function, so treat its score as a measurement with error.
        var options = new ChatCompletionOptions { Temperature = AppSettings.Current.OpenAI.Temperature };

        // WHY JSON mode for judges: we need fields we can read in code (score, verdict,
        // winner), not prose. JSON mode makes valid JSON far more likely, but the parsing
        // below still assumes it can go wrong. The answerer passes json: false.
        if (json)
            options.ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat();

        var completion = await client.CompleteChatAsync(messages, options);
        return completion.Value.Content.Count > 0
            ? completion.Value.Content[0].Text ?? ""
            : "";
    }

    // Returns null instead of throwing. Callers turn null into score 0 ("judge failed"),
    // so one bad reply shows up in the table instead of stopping the whole eval run.
    public static JsonDocument? Parse(string text)
    {
        try
        {
            return JsonDocument.Parse(Unwrap(text));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // WHY both casings: the prompt asks for camelCase keys, but models sometimes reply
    // with PascalCase ("Faithfulness" instead of "faithfulness"). Accept either.
    public static JsonElement? Prop(JsonElement element, string camelName)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        if (element.TryGetProperty(camelName, out var value)) return value;

        var pascal = char.ToUpperInvariant(camelName[0]) + camelName[1..];
        if (element.TryGetProperty(pascal, out value)) return value;
        return null;
    }

    public static string ReadString(JsonElement element, string name)
    {
        var prop = Prop(element, name);
        if (prop is null) return "";
        return prop.Value.ValueKind == JsonValueKind.String
            ? prop.Value.GetString() ?? ""
            : prop.Value.ToString();
    }

    // Reads one rubric criterion. The prompt asks for {"reason": "...", "score": n},
    // but a model may shortcut to a bare number ("faithfulness": 4) or a string ("4").
    // Every shape the code cannot read becomes score 0: a missing measurement.
    public static RubricMark ReadMark(JsonElement parent, string name)
    {
        var prop = Prop(parent, name);
        if (prop is null) return new RubricMark(0, "");

        var el = prop.Value;
        if (el.ValueKind is JsonValueKind.Number or JsonValueKind.String)
            return new RubricMark(Clamp(el), "");

        if (el.ValueKind != JsonValueKind.Object)
            return new RubricMark(0, "");

        var scoreProp = Prop(el, "score");
        var score = scoreProp is null ? 0 : Clamp(scoreProp.Value);
        return new RubricMark(score, ReadString(el, "reason"));
    }

    private static int Clamp(JsonElement el)
    {
        double n = el.ValueKind switch
        {
            JsonValueKind.Number => el.GetDouble(),
            JsonValueKind.String when double.TryParse(
                el.GetString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed) => parsed,
            _ => 0,
        };

        // CRITICAL: 0 is reserved for "judge failed", so it must never look like a real
        // score. Anything else is rounded and clamped to the rubric's 1-5 range, which
        // also absorbs replies like 4.5 or 7. See 05-AIEvaluation.md §8.
        if (n <= 0) return 0;
        return Math.Clamp((int)Math.Round(n), 1, 5);
    }

    // Models sometimes wrap JSON in prose or a ```json fence ("Here is my grade: {...}").
    // Keep only the outermost {...} so the parser sees just the object.
    private static string Unwrap(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start >= 0 && end > start)
            return text[start..(end + 1)];
        return text;
    }
}
