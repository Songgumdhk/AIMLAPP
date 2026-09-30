using AIMLAPP.Learning.AgenticWorkflowsDemo.Models;

namespace AIMLAPP.Learning.AgenticWorkflowsDemo.ConsoleUi;

public static class WorkflowPrinter
{
    public static void PrintNew(WorkflowState state, int traceCountBefore)
    {
        for (var i = traceCountBefore; i < state.Trace.Count; i++)
            Console.WriteLine("  " + state.Trace[i]);

        if (state.Trace.Skip(traceCountBefore).Any(line => line.StartsWith("[research]", StringComparison.Ordinal))
            && state.Scratchpad.Count > 0)
        {
            Console.WriteLine("    scratchpad: " + OneLine(state.Scratchpad[^1]));
        }

        var plan = state.Plan.Count == 0
            ? "(no plan)"
            : string.Join(" ", state.Plan.Select(s => $"{s.Action}={s.Status}"));
        Console.WriteLine(
            $"    plan: {plan} | notes={state.Scratchpad.Count} score={state.ReviewScore} status={state.Status}");
    }

    public static void PrintFinal(WorkflowState state, string? savedPath)
    {
        Console.WriteLine();
        Console.WriteLine($"=== run {state.RunId} — {state.Status} ===");
        Console.WriteLine("Trace:");
        foreach (var line in state.Trace)
            Console.WriteLine("  " + line);

        if (state.Scratchpad.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Scratchpad (short-term, this run):");
            for (var i = 0; i < state.Scratchpad.Count; i++)
                Console.WriteLine($"  [{i + 1}] {OneLine(state.Scratchpad[i])}");
        }

        if (!string.IsNullOrWhiteSpace(state.Draft))
        {
            Console.WriteLine();
            Console.WriteLine("Final draft:");
            Console.WriteLine(state.Draft);
        }

        if (!string.IsNullOrEmpty(savedPath))
            Console.WriteLine($"\nState file: {savedPath}");
    }

    private static string OneLine(string text)
    {
        var flat = text.Replace("\r", " ").Replace("\n", " ");
        return flat.Length <= 140 ? flat : flat[..140] + "...";
    }
}
