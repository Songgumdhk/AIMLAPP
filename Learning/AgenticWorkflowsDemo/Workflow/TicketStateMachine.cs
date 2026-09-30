namespace AIMLAPP.Learning.AgenticWorkflowsDemo.Workflow;

// A STATE MACHINE WITH NO MODEL.
// Each method is a node. It mutates TicketState, prints the snapshot, and
// returns the name of the next node. That return value is the edge.
// Loops and branches are just different return values — there is no LLM involved.
// Menu option 2. See 04-AgenticWorkflows.md §3. BriefWorkflow uses the same pattern
// once models sit inside some of the nodes.
public static class TicketStateMachine
{
    // The state every node shares. Routing reads these fields, not a chat transcript.
    public sealed class TicketState
    {
        public string Status { get; set; } = "new";
        public string Category { get; set; } = "";
        public string Owner { get; set; } = "";
        public int Attempt { get; set; }
    }

    public static void Run(string category, bool failFirstAttempt)
    {
        var state = new TicketState();
        string? node = "intake";
        var guard = 0;

        // THE WHOLE ENGINE: run the current node, get back the next node's name, repeat.
        // WHY the guard: a graph that can jump backwards needs a cap, same as MAX_TURNS.
        while (node is not null && guard++ < 12)
        {
            // Only names listed here are legal nodes. An unknown name maps to null,
            // which ends the graph instead of wandering into an undefined transition.
            node = node switch
            {
                "intake" => Intake(state),
                "classify" => Classify(state, category),
                "billing" => Assign(state, "BillingDesk"),
                "technical" => Assign(state, "TechDesk"),
                "general" => Assign(state, "GeneralDesk"),
                "resolve" => Resolve(state, failFirstAttempt),
                "escalate" => Finish(state, "escalated"),
                "close" => Finish(state, "closed"),
                _ => null
            };
        }
    }

    private static string Intake(TicketState state)
    {
        state.Status = "open";
        Print("intake", state, "classify");
        return "classify";
    }

    // CONDITIONAL ROUTING — the next node depends on data in the state.
    private static string Classify(TicketState state, string category)
    {
        state.Category = category.Trim().ToLowerInvariant();
        var next = state.Category switch
        {
            "billing" => "billing",
            "technical" => "technical",
            "general" => "general",
            // Anything unrecognised goes to a person rather than a guessed desk.
            _ => "escalate"
        };
        Print("classify", state, next);
        return next;
    }

    private static string Assign(TicketState state, string owner)
    {
        state.Owner = owner;
        Print(state.Category, state, "resolve");
        return "resolve";
    }

    // LOOP — if the first attempt fails, go back to classify instead of close.
    private static string Resolve(TicketState state, bool failFirstAttempt)
    {
        state.Attempt++;
        var failed = failFirstAttempt && state.Attempt == 1;
        var next = failed ? "classify" : "close";
        Print("resolve", state, next + (failed ? "   (loop: first attempt failed)" : ""));
        return next;
    }

    // Terminal node. Returning null is how a node says "no outgoing edge".
    private static string? Finish(TicketState state, string status)
    {
        state.Status = status;
        Print(status == "closed" ? "close" : "escalate", state, "(end)");
        return null;
    }

    private static void Print(string node, TicketState state, string next)
    {
        Console.WriteLine(
            $"  {node,-10} status={state.Status,-10} category={state.Category,-10} " +
            $"owner={state.Owner,-12} attempt={state.Attempt}  → {next}");
    }
}
