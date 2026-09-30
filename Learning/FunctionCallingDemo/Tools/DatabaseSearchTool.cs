using System.Text.Json;

namespace AIMLAPP.Learning.FunctionCallingDemo.Tools;

// Fake database search. Returns fixed rows so you can see how structured
// JSON results flow back into the LLM's response.
public static class DatabaseSearchTool
{
    public const string Name = "search_database";

    public static OpenAI.Chat.ChatTool Schema => OpenAI.Chat.ChatTool.CreateFunctionTool(
        functionName: Name,
        functionDescription:
            "Look up employees by department. Use this when the user asks " +
            "who works in a given team, department, or group.",
        // "enum" constrains the model to departments that actually exist, so it
        // can't invent "Marketing". Even a forced call (Exercise #3) picks one.
        functionParameters: BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "department": {
                    "type": "string",
                    "enum": ["Engineering", "Sales", "HR"],
                    "description": "The department name to search."
                }
            },
            "required": ["department"]
        }
        """));

    public static Task<string> ExecuteAsync(JsonElement args)
    {
        var department = args.GetProperty("department").GetString() ?? "";

        var employees = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Engineering"] = new[] { "Alice (Senior)", "Bob (Junior)", "Charlie (Lead)" },
            ["Sales"] = new[] { "Diana (Manager)", "Eve (Rep)" },
            ["HR"] = new[] { "Frank (Director)" }
        };

        // "Nothing found" is a normal answer, not an error. Say so plainly so the
        // model can tell the user instead of retrying.
        if (!employees.TryGetValue(department, out var list))
        {
            return Task.FromResult($"No employees found in department '{department}'.");
        }

        var json = JsonSerializer.Serialize(new { department, employees = list });
        return Task.FromResult(json);
    }
}
