namespace AIMLAPP.Learning.AgenticWorkflowsDemo.Models;

// One step in a plan. The action is the edge: it names the next node.
// Status is how the router knows whether the step still needs to run.
// "pending" or "done". The model never sets Status; nodes and code rules do.
public sealed class PlanStep
{
    public string Action { get; set; } = "";
    public string Instruction { get; set; } = "";
    public string Status { get; set; } = "pending";
}

// THE SHARED STATE — every node reads this and writes back to it.
// Nothing is passed as a loose string between agents. If it matters, it lives here.
// This object is also what gets saved to disk, so a paused run can resume later.
// WHY state instead of chat history: each agent reads only the fields it needs, and you
// can open the JSON and name which node wrote each field. See 04-AgenticWorkflows.md §5.
public sealed class WorkflowState
{
    public string RunId { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Goal { get; set; } = "";

    // Where to resume. Every node sets this to the next node's name (the edge).
    public string CurrentNode { get; set; } = "plan";

    // The engine loop only keeps going while this is "running". Other values used:
    // "waiting_for_human", "completed", "rejected", "stopped" (a loop cap was hit).
    public string Status { get; set; } = "running";

    // Written by the planner, read by the router. Execution progress lives on each step.
    public List<PlanStep> Plan { get; set; } = [];

    // Short-term memory: notes from this run only. It is saved in this run's checkpoint
    // (so a resume keeps it), but never reaches long-term memory unless you copy it out.
    public List<string> Scratchpad { get; set; } = [];

    // Snapshot of long-term facts loaded at the start of the run.
    // Copied onto the state so the checkpoint shows exactly what the writer was allowed to see.
    public List<string> MemoryFacts { get; set; } = [];

    public string Draft { get; set; } = "";

    // 0 means "not reviewed since the last draft". The writer resets it on every new draft
    // so a stale score can never let fresh text skip review.
    public int ReviewScore { get; set; }
    public string ReviewNotes { get; set; } = "";
    public int RevisionCount { get; set; }
    public List<string> HumanFeedback { get; set; } = [];

    // One line per node. Saved with the state, so the JSON explains every hop.
    public List<string> Trace { get; set; } = [];
}
