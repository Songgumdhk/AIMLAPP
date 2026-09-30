// =============================================================================
//  Chapter 1 — Function Calling / Tool Calling Demo (entry point)
// =============================================================================
//
//  PURPOSE
//  -------
//  Orchestrate the raw OpenAI function-calling loop. Each tool, the catalog,
//  the dispatcher, and the loop live in their own class.
//
//  Folder map (matches 01-FunctionCalling.md):
//    Tools/   one class per tool (schema + implementation) + ToolCatalog + ApprovalPolicy
//    Loop/    FunctionCallingLoop (the agent loop) + ToolDispatcher
//
//  HOW TO RUN
//  ----------
//  From Program.cs, menu option 5.
// =============================================================================

using OpenAI.Chat;
using AIMLAPP.Configuration;
using AIMLAPP.Learning.FunctionCallingDemo.Loop;
using AIMLAPP.Learning.FunctionCallingDemo.Tools;

namespace AIMLAPP.Learning.FunctionCallingDemo;

public static class FunctionCallingDemo
{
    // SAFETY CAP — without this, a badly-behaved LLM (or a broken tool)
    // could loop forever and burn your API budget. Exercise #5 lets you override.
    // Default cap comes from appsettings.json → FunctionCalling:MaxTurns.
    private static int DefaultMaxTurns => AppSettings.Current.FunctionCalling.MaxTurns;

    public static async Task RunAsync(string apiKey)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // Raw OpenAI SDK, no framework. The model comes from appsettings.json →
        // OpenAI:ChatModel and must support tool calling.
        var client = new ChatClient(model: AppSettings.Current.OpenAI.ChatModel, apiKey);

        // EXERCISE #5 — Try `1` and give it a task that needs 3 sequential calls.
        Console.WriteLine("=== Function Calling Demo ===\n");
        Console.Write($"MAX_TURNS (press Enter for default {DefaultMaxTurns}): ");
        var turnsInput = Console.ReadLine();
        int maxTurns = int.TryParse(turnsInput, out var t) && t > 0 ? t : DefaultMaxTurns;

        // EXERCISE #3 — 'auto' is what you'll use 95% of the time.
        Console.WriteLine("Tool-choice mode:");
        Console.WriteLine("  1) auto (default) — LLM decides");
        Console.WriteLine("  2) force search_database (Exercise #3)");
        Console.WriteLine("  3) none — forbid all tool calls");
        Console.Write("Selection (Enter for 1): ");
        var modeInput = Console.ReadLine()?.Trim();

        // Tools live on the request options, so the full catalog is sent with
        // EVERY call. The model does not remember tools between requests.
        var options = new ChatCompletionOptions();
        foreach (var tool in ToolCatalog.Build())
        {
            options.Tools.Add(tool);
        }

        // tool_choice (§4). Leaving ToolChoice unset means "auto": the model decides.
        switch (modeInput)
        {
            case "2":
                // Forces this exact tool even when it makes no sense ("Hi"),
                // so the model invents arguments. Only for deterministic pipelines.
                options.ToolChoice = ChatToolChoice.CreateFunctionChoice(DatabaseSearchTool.Name);
                Console.WriteLine("[Forcing tool: search_database]");
                break;
            case "3":
                // The model still SEES the tools but may not call them: text only.
                options.ToolChoice = ChatToolChoice.CreateNoneChoice();
                Console.WriteLine("[Tool use disabled]");
                break;
            default:
                Console.WriteLine("[Tool choice: auto]");
                break;
        }

        Console.WriteLine($"[MAX_TURNS: {maxTurns}]\n");
        Console.WriteLine("Try prompts like:");
        Console.WriteLine("  * What's the weather in Sydney and Tokyo?          (parallel tool calls)");
        Console.WriteLine("  * What are the prices of MSFT and GOOG?            (Exercise #1)");
        Console.WriteLine("  * What's the weather in Atlantis?                  (Exercise #2 - error)");
        Console.WriteLine("  * Find employees in Engineering.                   (single tool call)");
        Console.WriteLine("  * Delete record 42 from the customers table.       (Exercise #4 - HITL)");
        Console.WriteLine("  * Find the HR director's email and send them a     (Exercise #5 - needs");
        Console.WriteLine("    note asking about vacation policy.                3 sequential calls)");
        Console.WriteLine("Type 'exit' to quit.\n");

        // The conversation history. It lives outside the chat loop so every user
        // turn, tool call and tool result accumulates here and is resent each time.
        // The system prompt reinforces what the tool descriptions say about
        // parallel calls.
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(
                "You are a helpful assistant. Use the provided tools when they help. " +
                "When the user asks about multiple items, prefer parallel tool calls."),
        };

        while (true)
        {
            Console.Write("\nYou: ");
            var userInput = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(userInput)) continue;
            if (userInput.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

            messages.Add(new UserChatMessage(userInput));
            await FunctionCallingLoop.RunTurnAsync(client, messages, options, maxTurns);
        }
    }
}
