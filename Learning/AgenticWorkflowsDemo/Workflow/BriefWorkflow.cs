using OpenAI.Chat;
using AIMLAPP.Learning.AgenticWorkflowsDemo.Agents;
using AIMLAPP.Learning.AgenticWorkflowsDemo.Models;

namespace AIMLAPP.Learning.AgenticWorkflowsDemo.Workflow;

// THE GRAPH for the onboarding-brief workflow.
// One call to StepAsync runs the current node, then sets CurrentNode to whatever
// comes next. The caller loops while Status == "running" and saves state each time.
//
//   plan → route → research → route → draft → route → review
//                    ▲                              │
//                    └──────── if score is low ─────┘
//   route (nothing left) → hitl → (pause)
//
// Every node writes the next node's name into state.CurrentNode. That assignment is the edge.
// See 04-AgenticWorkflows.md §2, §3 and §10.
public static class BriefWorkflow
{
    // WHY one node per call: the caller saves a checkpoint between every step,
    // so a crash loses at most one node of work (§9).
    public static async Task StepAsync(ChatClient client, WorkflowState state)
    {
        switch (state.CurrentNode)
        {
            // STEP 1: The model proposes the steps. Code owns the list from here on.
            case "plan":
                Console.WriteLine("  … planner is writing steps");
                state.Plan = await PlannerAgent.CreatePlanAsync(client, state.Goal);
                state.Trace.Add("[plan] " + string.Join(" → ", state.Plan.Select(s => s.Action)));
                state.CurrentNode = "route";
                break;

            // STEP 2: The router. Boring on purpose: no model call, just "first pending step".
            // WHY: a code router is predictable. Its trace always follows the plan (§8).
            // When nothing is pending, every stage is done, so the graph goes to the human.
            case "route":
                var pending = state.Plan.FirstOrDefault(s => s.Status == "pending");
                var next = pending?.Action ?? "hitl";
                state.Trace.Add("[route] next=" + next);
                state.CurrentNode = next;
                break;

            // STEP 3: Worker nodes. Each specialist updates state and marks its step done,
            // then control always returns to "route". Specialists never call each other.
            case "research":
                Console.WriteLine("  … researcher is searching the handbook");
                await ResearcherAgent.RunAsync(client, state);
                state.CurrentNode = "route";
                break;

            case "draft":
                Console.WriteLine("  … writer is drafting");
                await WriterAgent.RunAsync(client, state);
                state.CurrentNode = "route";
                break;

            case "review":
                Console.WriteLine("  … reviewer is scoring");
                await ReviewerAgent.RunAsync(client, state);
                // The reviewer only wrote a score. ApplyReview is code that decides the edge.
                ApplyReview(state);
                state.CurrentNode = "route";
                break;

            // STEP 4: Pause before any side effect. Changing Status (not CurrentNode) is what
            // stops the caller's loop. No model call happens until a person answers (§7).
            case "hitl":
                state.Status = "waiting_for_human";
                state.Trace.Add("[hitl] paused for a human");
                break;

            // An unknown node name (for example a hand-edited checkpoint) ends the run
            // loudly instead of guessing where to go.
            default:
                state.Trace.Add($"[engine] unknown node '{state.CurrentNode}'");
                state.Status = "rejected";
                break;
        }
    }

    // The reviewer only produced a score. This method is the conditional edge.
    // WHY code and not the reviewer: a model grading its own pass/fail is easy to fool.
    // The score is data; a fixed threshold turns it into a route (§8, §12).
    public static void ApplyReview(WorkflowState state)
    {
        // Threshold and cap come from appsettings.json → AgenticWorkflows:ReviewPassScore
        // and AgenticWorkflows:MaxRevisions.
        var passed = state.ReviewScore >= WorkflowLimits.ReviewPassScore;
        var canRevise = state.RevisionCount < WorkflowLimits.MaxRevisions;

        if (!passed && canRevise)
        {
            // THE LOOP: nothing jumps backwards directly. Re-opening the steps is enough,
            // because the next visit to "route" will find "draft" pending again.
            state.RevisionCount++;
            foreach (var step in state.Plan.Where(s => s.Action is "draft" or "review"))
                step.Status = "pending";
            state.Trace.Add($"[route-rule] score below {WorkflowLimits.ReviewPassScore}; revision {state.RevisionCount} goes back to the writer");
            return;
        }

        // Passed, or out of revisions. Either way, leave the loop and let the human decide.
        var reviewStep = state.Plan.FirstOrDefault(s => s.Action == "review" && s.Status == "pending");
        if (reviewStep is not null) reviewStep.Status = "done";
        state.Trace.Add(passed
            ? "[route-rule] score passed; leaving the review loop"
            : "[route-rule] revision cap reached; leaving the review loop");
    }

    // Human asked for changes. Re-open draft and review, then let the router continue.
    // Human feedback was already written to state.HumanFeedback by HumanGate; the writer reads it.
    public static void RequestChanges(WorkflowState state)
    {
        state.RevisionCount++;
        // CRITICAL: clear the stale score so an old "8" cannot let the new draft skip review (§5).
        state.ReviewScore = 0;
        foreach (var step in state.Plan.Where(s => s.Action is "draft" or "review"))
            step.Status = "pending";
        state.Status = "running";
        state.CurrentNode = "route";
        state.Trace.Add("[hitl] changes requested");
    }
}
