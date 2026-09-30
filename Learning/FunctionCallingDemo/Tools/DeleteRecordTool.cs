using System.Text.Json;

namespace AIMLAPP.Learning.FunctionCallingDemo.Tools;

// EXERCISE #4 — delete_record with human approval.
// Ask: "Delete record 42 from the customers table." Deny it and watch the LLM adapt.
public static class DeleteRecordTool
{
    public const string Name = "delete_record";

    public static OpenAI.Chat.ChatTool Schema => OpenAI.Chat.ChatTool.CreateFunctionTool(
        functionName: Name,
        // Prompts are not a security boundary. The description discourages casual
        // use, but ApprovalPolicy is what actually stops the delete.
        functionDescription:
            "Permanently delete a record from a database table. This action " +
            "is DESTRUCTIVE and cannot be undone. Only use when the user " +
            "explicitly requests deletion, and always confirm with the user first.",
        functionParameters: BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "table": {
                    "type": "string",
                    "description": "The database table name, e.g. 'customers'."
                },
                "id": {
                    "type": "integer",
                    "description": "The primary key ID of the record to delete."
                }
            },
            "required": ["table", "id"]
        }
        """));

    public static Task<string> ExecuteAsync(JsonElement args)
    {
        var table = args.GetProperty("table").GetString();
        // If the model sends "42" (a string) instead of 42, GetInt32 throws; the
        // dispatcher turns that into an ERROR result the model can correct (§5, §8).
        var id = args.GetProperty("id").GetInt32();

        Console.WriteLine($"  [SIMULATED DELETE] table={table} id={id}");
        return Task.FromResult($"Deleted record id={id} from table '{table}'. This cannot be undone.");
    }
}
