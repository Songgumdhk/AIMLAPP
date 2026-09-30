using Microsoft.SemanticKernel;

namespace AIMLAPP.Learning.SemanticKernelDemo.Filters;

// HUMAN-IN-THE-LOOP AS MIDDLEWARE — see 02-SemanticKernel.md §4.3 and §7.
// Same behavior as Chapter 1's RequiresHumanApproval check, but centralized
// as middleware. Every dangerous tool goes through here without needing to
// know about approval in its own code.
// An IFunctionInvocationFilter wraps every function call SK makes, including
// the ones it auto-invokes for the LLM, like ASP.NET Core middleware wraps requests.
public sealed class ApprovalFilter : IFunctionInvocationFilter
{
    // Matches the short function name ("send_email"), not the plugin-prefixed
    // name the model sees, so it works no matter which plugin hosts the tool.
    private static readonly HashSet<string> Dangerous =
        new(StringComparer.OrdinalIgnoreCase) { "send_email", "delete_record" };

    public async Task OnFunctionInvocationAsync(
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, Task> next)
    {
        // Safe tools pass straight through. Forgetting next() here would silently
        // block every tool (§11).
        if (!Dangerous.Contains(context.Function.Name))
        {
            await next(context);
            return;
        }

        Console.WriteLine();
        Console.WriteLine($"[HUMAN APPROVAL REQUIRED]");
        Console.WriteLine($"  Tool: {context.Function.PluginName}.{context.Function.Name}");
        Console.WriteLine($"  Args: {string.Join(", ", context.Arguments.Select(a => $"{a.Key}={a.Value}"))}");
        Console.Write("  Approve? (y/n): ");
        var approval = Console.ReadLine()?.Trim().ToLowerInvariant();

        if (approval != "y" && approval != "yes")
        {
            // SHORT-CIRCUIT: set the result BEFORE calling next().
            // We do NOT call next() here — the real function never runs.
            // The LLM sees "DENIED..." as the tool result and adapts.
            context.Result = new FunctionResult(
                context.Function,
                "DENIED: The user declined to approve this action. Do not retry the same action.");
            return;
        }

        // Approved: continue down the pipeline to the real function.
        await next(context);
    }
}
