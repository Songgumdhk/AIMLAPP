using System.Text.Json;

namespace AIMLAPP.Learning.FunctionCallingDemo.Tools;

// Helper for Exercise #5 (multi-turn chaining).
// Must be called AFTER search_database, so it forces a sequential chain
// that MAX_TURNS=1 will break.
public static class EmployeeEmailTool
{
    public const string Name = "get_employee_email";

    public static OpenAI.Chat.ChatTool Schema => OpenAI.Chat.ChatTool.CreateFunctionTool(
        functionName: Name,
        // Naming search_database in the description hints at the chain: the
        // model learns it must look the name up first, one turn at a time (§6).
        functionDescription:
            "Look up an employee's email address by their name. " +
            "Requires knowing the employee name first — usually obtained " +
            "from search_database.",
        functionParameters: BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "name": {
                    "type": "string",
                    "description": "Employee full or first name, e.g. 'Frank'."
                }
            },
            "required": ["name"]
        }
        """));

    public static Task<string> ExecuteAsync(JsonElement args)
    {
        var rawName = args.GetProperty("name").GetString() ?? "";
        // Be tolerant of model input: it often passes the name exactly as
        // search_database returned it, e.g. "Frank (Director)".
        var name = rawName.Split('(')[0].Trim();

        var emails = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Alice"] = "alice@company.com",
            ["Bob"] = "bob@company.com",
            ["Charlie"] = "charlie@company.com",
            ["Diana"] = "diana@company.com",
            ["Eve"] = "eve@company.com",
            ["Frank"] = "frank@company.com",
        };

        return Task.FromResult(emails.TryGetValue(name, out var email)
            ? email
            : $"No email on file for '{name}'.");
    }
}
