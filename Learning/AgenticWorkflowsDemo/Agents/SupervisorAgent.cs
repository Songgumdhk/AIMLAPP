using OpenAI.Chat;
using AIMLAPP.Learning.AgenticWorkflowsDemo.Knowledge;
using AIMLAPP.Learning.AgenticWorkflowsDemo.Models;
using AIMLAPP.Learning.AgenticWorkflowsDemo.Workflow;

namespace AIMLAPP.Learning.AgenticWorkflowsDemo.Agents;

// SUPERVISOR — an agent whose only job is to pick the next agent.
// Mode 5 uses this. The full workflow (mode 6) routes from the plan in code instead,
// which is the more reliable of the two patterns.
// Model-routed is flexible but easier to fool; the hop cap in the caller
// (appsettings.json → AgenticWorkflows:MaxAgentHops) is what stops it cycling. See §8.
public static class SupervisorAgent
{
    // Reason is logged to the trace so you can see why each hop was chosen.
    public sealed record Decision(string Next, string Reason);

    public static async Task<Decision> DecideAsync(ChatClient client, WorkflowState state)
    {
        // WHY a summary, not the full draft: the supervisor only needs signals to route
        // (counts, score, revisions). A narrow input keeps it from doing the writer's job.
        // The pass score and revision cap come from WorkflowLimits (appsettings.json), so the
        // supervisor follows the same rules as the code-routed graph in mode 6.
        // $$""" means {{...}} interpolates while single braces stay literal JSON.
        var passScore = WorkflowLimits.ReviewPassScore;
        var maxRevisions = WorkflowLimits.MaxRevisions;
        var raw = await ChatHelper.CompleteAsync(
            client,
            $$"""
            You are a supervisor agent. Pick the next specialist.
            Output only JSON: {"next":"researcher"|"writer"|"reviewer"|"done","reason":"short"}
            Rules:
            - researcher when the scratchpad is empty
            - writer when there is no draft, or the review score is below {{passScore}} and revisions are under {{maxRevisions}}
            - reviewer when a draft exists and the review score is 0 (not reviewed since the last write)
            - done only when a draft exists and the review score is {{passScore}} or higher
            """,
            $"""
            Goal: {state.Goal}
            Scratchpad entries: {state.Scratchpad.Count}
            Draft length: {state.Draft.Length}
            Review score: {state.ReviewScore}
            Revisions so far: {state.RevisionCount}
            Review notes: {state.ReviewNotes}
            """);

        // CRITICAL: never trust the routing output blindly. Unparseable JSON or an unknown
        // agent name fails safe to "done" instead of calling something that does not exist.
        using var doc = ChatHelper.ParseOrNull(raw);
        if (doc is null)
            return new Decision("done", "supervisor JSON was unusable; stopping");

        {
            var next = doc.RootElement.TryGetProperty("next", out var n)
                ? n.GetString()?.Trim().ToLowerInvariant()
                : null;
            var reason = doc.RootElement.TryGetProperty("reason", out var r)
                ? r.GetString() ?? ""
                : "";

            if (next is not ("researcher" or "writer" or "reviewer" or "done"))
                return new Decision("done", "supervisor returned an unknown agent; stopping");

            return new Decision(next, reason);
        }
    }
}
