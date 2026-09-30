using OpenAI.Chat;
using AIMLAPP.Learning.AgenticWorkflowsDemo.Models;

namespace AIMLAPP.Learning.AgenticWorkflowsDemo.Agents;

// PLANNING — the model writes the steps. It does not run them.
// The graph only knows three actions, so unknown actions are dropped and
// any missing stage is filled in. The plan is data on the state, not a hidden prompt.
// See 04-AgenticWorkflows.md §4.
public static class PlannerAgent
{
    public static async Task<List<PlanStep>> CreatePlanAsync(ChatClient client, string goal)
    {
        // STEP 1: Ask for JSON in a fixed shape (json: true turns on JSON mode).
        // WHY JSON: code has to read the plan and route on it. Prose cannot be validated.
        // The prompt is narrow on purpose: list steps, use three actions, nothing else.
        var raw = await ChatHelper.CompleteAsync(
            client,
            "You break a user goal into an ordered plan. Output only JSON.",
            "Return {\"steps\":[{\"action\":\"research\"|\"draft\"|\"review\",\"instruction\":\"...\"}]}\n" +
            "Use only those three actions. Gather facts before writing. One step per action is enough.\n" +
            "Goal: " + goal,
            json: true);

        // STEP 2: Validate before anything runs. Bad or unknown steps are dropped here,
        // so the router never sees an action the graph has no node for.
        var steps = Parse(raw);
        if (steps.Count == 0)
            Console.WriteLine("[plan] model JSON was unusable; using the default 3-step plan.");

        // STEP 3: Guarantee the stages the graph depends on. Research is inserted first
        // (facts before writing), and review is always present so no draft skips scoring.
        EnsureStage(steps, "research", $"Gather the handbook facts required for: {goal}", true);
        EnsureStage(steps, "draft", $"Write the deliverable for: {goal}", false);
        EnsureStage(steps, "review", "Score the draft for completeness and faithfulness to the research notes.", false);
        return steps;
    }

    private static List<PlanStep> Parse(string raw)
    {
        var steps = new List<PlanStep>();

        using var document = ChatHelper.ParseOrNull(raw);

        if (document is null)
            return steps;

        if (!document.RootElement.TryGetProperty("steps", out var stepsArray))
            return steps;

        foreach (var element in stepsArray.EnumerateArray())
        {
            var action = element.TryGetProperty("action", out var actionProperty)
                ? Normalize(actionProperty.GetString())
                : null;

            var instruction = element.TryGetProperty("instruction", out var instructionProperty)
                ? instructionProperty.GetString() ?? string.Empty
                : string.Empty;

            // A step with no usable action or instruction cannot be executed or explained.
            if (action is null || string.IsNullOrWhiteSpace(instruction))
                continue;

            steps.Add(new PlanStep
            {
                Action = action,
                Instruction = instruction.Trim()
            });
        }

        return steps;
    }

    // Models paraphrase. Map common synonyms onto the three node names; anything else
    // becomes null and is dropped. This is the allow-list for the graph.
    private static string? Normalize(string? action) => action?.Trim().ToLowerInvariant() switch
    {
        "research" or "search" or "lookup" => "research",
        "draft" or "write" or "compose" => "draft",
        "review" or "evaluate" or "check" => "review",
        _ => null
    };


    private static void EnsureStage(List<PlanStep> steps, string action, string instruction, bool atStart)
    {
        bool stageExists = steps.Any(step => step.Action == action);

        if (stageExists)
            return;

        var newStep = new PlanStep
        {
            Action = action,
            Instruction = instruction
        };

        if (atStart)
        {
            steps.Insert(0, newStep);
            return;
        }

        steps.Add(newStep);
    }


}