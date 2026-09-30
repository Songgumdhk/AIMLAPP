using System.Text.Json;
using OpenAI.Chat;
using AIMLAPP.Learning.FunctionCallingDemo.Tools;

namespace AIMLAPP.Learning.FunctionCallingDemo.Loop;

// THE TOOL DISPATCHER — turns the LLM's tool request into a real C# call.
// Given a tool call, run the right C# function.
// ALWAYS returns a string — even for errors — so the LLM can adapt.
// Never throw out of here. See 01-FunctionCalling.md §8 (tool errors).
public static class ToolDispatcher
{
    public static async Task<string> ExecuteAsync(ChatToolCall toolCall)
    {
        try
        {
            // STEP 1: Human-in-the-loop gate (§10). It runs BEFORE any work is done,
            // so a denied action never touches the real system.
            // Caveat: calls run in parallel, so two approval prompts in one turn
            // could interleave on the console. Fine for a demo; a real app would
            // queue approvals.
            if (ApprovalPolicy.RequiresHumanApproval(toolCall.FunctionName))
            {
                Console.WriteLine();
                Console.WriteLine($"[HUMAN APPROVAL REQUIRED]");
                Console.WriteLine($"  Tool: {toolCall.FunctionName}");
                Console.WriteLine($"  Args: {toolCall.FunctionArguments}");
                Console.Write("  Approve? (y/n): ");
                var approval = Console.ReadLine()?.Trim().ToLowerInvariant();
                if (approval != "y" && approval != "yes")
                {
                    // WHY return instead of throw: the denial becomes the tool result,
                    // so the LLM can explain it to the user. "Do not retry" stops it
                    // from immediately asking again.
                    return "DENIED: The user declined to approve this action. Do not retry the same action.";
                }
            }

            // STEP 2: Parse the arguments. They arrive as a JSON STRING written by
            // the model (§5), so they can be malformed or missing fields. Any
            // parse failure is caught below and fed back to the model.
            using var argsDoc = JsonDocument.Parse(toolCall.FunctionArguments);
            var args = argsDoc.RootElement;

            // STEP 3: Route by name. This hand-written switch is what SK replaces
            // with automatic dispatch in Chapter 2 (02-SemanticKernel.md §2).
            return toolCall.FunctionName switch
            {
                WeatherTool.Name => await WeatherTool.ExecuteAsync(args),
                StockPriceTool.Name => await StockPriceTool.ExecuteAsync(args),
                DatabaseSearchTool.Name => await DatabaseSearchTool.ExecuteAsync(args),
                EmployeeEmailTool.Name => await EmployeeEmailTool.ExecuteAsync(args),
                SendEmailTool.Name => await SendEmailTool.ExecuteAsync(args),
                DeleteRecordTool.Name => await DeleteRecordTool.ExecuteAsync(args),
                // Models can hallucinate tool names. Tell it, don't crash.
                _ => $"ERROR: Unknown tool '{toolCall.FunctionName}'."
            };
        }
        catch (Exception ex)
        {
            // CRITICAL: convert every exception into a tool result. If we threw,
            // the app would crash and the LLM would never learn what went wrong.
            // Including the exception type helps the model decide whether to
            // retry with different arguments or give up (Exercise #2).
            return $"ERROR: {ex.GetType().Name}: {ex.Message}";
        }
    }
}
