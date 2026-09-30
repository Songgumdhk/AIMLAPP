using OpenAI.Chat;

namespace AIMLAPP.Learning.FunctionCallingDemo.Tools;

// The tool catalog you send to the LLM. Each tool owns its own schema;
// this class just collects them. The `description` on each schema is what
// the LLM reads to decide WHEN to call a tool.
// Every schema is sent on every request and costs prompt tokens, so keep the
// catalog focused: more tools also means more chances to pick the wrong one.
public static class ToolCatalog
{
    public static List<ChatTool> Build() =>
    [
        WeatherTool.Schema,
        StockPriceTool.Schema,
        DatabaseSearchTool.Schema,
        EmployeeEmailTool.Schema,
        SendEmailTool.Schema,
        DeleteRecordTool.Schema,
    ];
}
