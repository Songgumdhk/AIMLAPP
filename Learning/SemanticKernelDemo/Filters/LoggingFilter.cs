using Microsoft.SemanticKernel;

namespace AIMLAPP.Learning.SemanticKernelDemo.Filters;

// OBSERVABILITY AS MIDDLEWARE — proves SK really ran tools inside the one-liner.
// Prints one line before + after each tool call. This is how you'd hook up
// OpenTelemetry / Datadog / App Insights in production.
// Registered first, so it is the OUTERMOST filter: it times and logs everything
// inside it, including ApprovalFilter's prompt and any DENIED result (§7).
public sealed class LoggingFilter : IFunctionInvocationFilter
{
    public async Task OnFunctionInvocationAsync(
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, Task> next)
    {
        var fnName = $"{context.Function.PluginName}.{context.Function.Name}";
        var args = string.Join(", ", context.Arguments.Select(a => $"{a.Key}={a.Value}"));
        Console.WriteLine($"[FN ->] {fnName}({args})");

        var start = DateTime.UtcNow;
        // If the tool throws (Atlantis), the exception passes up through here, so
        // no [FN <-] line prints. SK catches it further out and tells the LLM.
        // Wrap next() in try/catch if you want to log failures too.
        await next(context);   // MUST call next(), or the tool never runs
        var elapsed = DateTime.UtcNow - start;

        // After next() returns, context.Result holds what the LLM will receive as
        // the tool result, whether it came from the function or an inner filter.
        var result = context.Result?.GetValue<object>()?.ToString() ?? "(null)";
        Console.WriteLine($"[FN <-] {fnName} = {Truncate(result, 120)}  ({elapsed.TotalMilliseconds:F0} ms)");
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "...";
}
