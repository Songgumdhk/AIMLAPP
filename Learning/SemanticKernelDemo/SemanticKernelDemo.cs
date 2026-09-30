// =============================================================================
//  Chapter 2 — Semantic Kernel Demo (entry point)
// =============================================================================
//
//  PURPOSE
//  -------
//  Orchestrate the Kernel: register plugins + filters, then run the chat loop.
//  Each plugin and filter lives in its own class under Learning/SemanticKernelDemo/.
//
//  Folder map (matches 02-SemanticKernel.md):
//    Plugins/   WeatherPlugin, StocksPlugin, HrPlugin, AdminPlugin
//    Filters/   LoggingFilter, ApprovalFilter (SK middleware)
//
//  HOW TO RUN
//  ----------
//  From Program.cs, menu option 6.
// =============================================================================

using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using AIMLAPP.Configuration;
using AIMLAPP.Learning.SemanticKernelDemo.Filters;
using AIMLAPP.Learning.SemanticKernelDemo.Plugins;

// SK is a preview API for some pieces — suppress the SKEXP warnings.
// SKEXP0001 = general experimental APIs (FunctionChoiceBehavior etc.)
#pragma warning disable SKEXP0001

namespace AIMLAPP.Learning.SemanticKernelDemo;

public static class SemanticKernelDemo
{
    public static async Task RunAsync(string apiKey)
    {
        // Force UTF-8 output so non-ASCII characters (°C, accents, any
        // LLM-generated Unicode) render correctly on Windows consoles.
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // STEP 1 — Build the Kernel.
        // This is essentially a DI container specialized for AI.
        var builder = Kernel.CreateBuilder();

        // Change this ONE line to switch providers:
        //   builder.AddAzureOpenAIChatCompletion("deployment", endpoint, key);
        //   builder.AddOllamaChatCompletion("llama3.1", new Uri("http://localhost:11434"));
        // Everything below (plugins, filters, history) stays the same (§10).
        // Model comes from appsettings.json → OpenAI:ChatModel (Exercise #4).
        builder.AddOpenAIChatCompletion(modelId: AppSettings.Current.OpenAI.ChatModel, apiKey: apiKey);

        // Register filters (SK's middleware). Order matters — LoggingFilter
        // wraps everything, ApprovalFilter runs inside it.
        // WHY this order: filters run in registration order, first = outermost
        // (§7). So even a DENIED call still shows up in the [FN] log lines.
        builder.Services.AddSingleton<IFunctionInvocationFilter, LoggingFilter>();
        builder.Services.AddSingleton<IFunctionInvocationFilter, ApprovalFilter>();

        Kernel kernel = builder.Build();

        // STEP 2 — Register plugins.
        // Notice: no BinaryData, no JSON schemas. Just C# classes.
        // SK reflects over each [KernelFunction] method and merges all plugins into
        // one tool catalog. The string is the plugin name; SK prefixes it to the
        // tool name the model sees (e.g. Weather-get_weather).
        // AddFromType suits these plugins because they hold no per-instance state.
        // A plugin that needs dependencies would use AddFromObject (§5, §11).
        kernel.Plugins.AddFromType<WeatherPlugin>("Weather");
        kernel.Plugins.AddFromType<StocksPlugin>("Stocks");
        kernel.Plugins.AddFromType<HrPlugin>("Hr");
        kernel.Plugins.AddFromType<AdminPlugin>("Admin");

        // STEP 3 — Get the chat service and set execution settings.
        // FunctionChoiceBehavior.Auto() = "let the LLM pick tools AND run
        // the whole tool loop for me". This one line replaces Chapter 1's for-loop.
        // It also replaces the dispatcher switch and the ToolChatMessage bookkeeping,
        // and SK keeps its own internal cap on round-trips, like MAX_TURNS.
        // Swap in Required() or None() to mirror Chapter 1's tool_choice modes (§6).
        // Note: the model may request several calls at once, but SK runs them one
        // after another unless FunctionChoiceBehaviorOptions allows concurrency.
        var chatService = kernel.GetRequiredService<IChatCompletionService>();

        var settings = new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
        };

        // STEP 4 — Conversation loop.
        // ChatHistory is SK's typed version of Chapter 1's List<ChatMessage> (§9).
        var history = new ChatHistory();
        history.AddSystemMessage(
            "You are a helpful assistant. Use the provided tools when they help. " +
            "When the user asks about multiple items, prefer parallel tool calls.");

        Console.WriteLine("=== Semantic Kernel Demo ===");
        Console.WriteLine("Same tools as Chapter 1, but SK runs the whole loop for you.");
        Console.WriteLine("The [FN] lines below come from LoggingFilter, proving tools were invoked.\n");
        Console.WriteLine("Try prompts like:");
        Console.WriteLine("  * What's the weather in Sydney, Tokyo and Paris?         (parallel)");
        Console.WriteLine("  * What are the prices of MSFT, GOOG and AAPL?             (parallel)");
        Console.WriteLine("  * What's the weather in Atlantis?                         (auto-error handling)");
        Console.WriteLine("  * Find the HR director's email and send them a note       (multi-turn)");
        Console.WriteLine("    asking about vacation policy.");
        Console.WriteLine("  * Delete record 42 from the customers table.              (HITL filter)");
        Console.WriteLine("Type 'exit' to quit.\n");

        while (true)
        {
            Console.Write("\nYou: ");
            var userInput = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(userInput)) continue;
            if (userInput.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

            history.AddUserMessage(userInput);

            // THE ONE-LINER — SK runs the entire tool loop.
            // Passing `kernel` is what gives SK access to the plugins and filters.
            // Without it the model has no tools to call.
            var reply = await chatService.GetChatMessageContentAsync(history, settings, kernel);

            Console.WriteLine($"\nAssistant: {reply.Content}");
            // With auto-invoke, SK appends the intermediate tool-call and tool-result
            // messages to `history` during the call. We add the final answer so
            // the next user turn has the full context.
            history.Add(reply);
        }
    }
}

#pragma warning restore SKEXP0001
