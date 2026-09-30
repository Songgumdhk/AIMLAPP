using AIMLAPP.Learning.AgenticWorkflowsDemo.Models;

namespace AIMLAPP.Learning.AgenticWorkflowsDemo.Workflow;

// HUMAN-IN-THE-LOOP — the graph stops on purpose. Nothing is "sent" until
// a person types approve. Edit writes feedback onto the state and the graph resumes.
// See 04-AgenticWorkflows.md §7. The run is already checkpointed before this prompt
// appears, so you can quit here and resume later.
public static class HumanGate
{
    // Returns a decision, not an action. The caller applies it to the state, so the gate
    // itself never sends, publishes, or calls a model.
    public static string Ask(WorkflowState state)
    {
        Console.WriteLine();
        Console.WriteLine("----- draft awaiting approval -----");
        Console.WriteLine(string.IsNullOrWhiteSpace(state.Draft) ? "(empty draft)" : state.Draft);
        Console.WriteLine("-----------------------------------");
        if (state.ReviewScore > 0)
            Console.WriteLine($"Review score: {state.ReviewScore} — {state.ReviewNotes}");

        while (true)
        {
            Console.Write("[a]pprove  [e]dit  [r]eject: ");
            var key = Console.ReadLine()?.Trim().ToLowerInvariant();
            switch (key)
            {
                case "a":
                case "approve":
                    return "approve";
                case "r":
                case "reject":
                    return "reject";
                case "e":
                case "edit":
                    // WHY on the state: the writer reads HumanFeedback on its next run,
                    // and the feedback survives a checkpoint/resume.
                    Console.Write("What should change? ");
                    var feedback = Console.ReadLine();
                    var feeds = string.IsNullOrWhiteSpace(feedback)
                        ? "Please revise."
                        : feedback.Trim();
                    state.HumanFeedback.Add(feeds);
                    return "edit";
                default:
                    Console.WriteLine("Type a, e, or r.");
                    break;
            }
        }
    }
}
