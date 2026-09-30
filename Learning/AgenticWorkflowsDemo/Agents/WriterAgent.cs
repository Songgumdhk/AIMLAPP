using OpenAI.Chat;
using AIMLAPP.Learning.AgenticWorkflowsDemo.Models;

namespace AIMLAPP.Learning.AgenticWorkflowsDemo.Agents;

// WRITER AGENT — turns state into a draft.
// It can see the scratchpad (this run) and long-term facts (previous runs).
// A new draft clears the old review score so the graph knows the text is stale.
// Narrow job: produce a draft. It never sends it; sending is a later step, after a human.
public static class WriterAgent
{
    public static async Task RunAsync(ChatClient client, WorkflowState state)
    {
        var step = state.Plan.FirstOrDefault(s => s.Action == "draft" && s.Status == "pending");
        var instruction = step?.Instruction ?? state.Goal;

        // STEP 1: Build the prompt from state fields, each in its own labelled section.
        // WHY labelled: the model can tell this-run facts (scratchpad) from saved
        // preferences (long-term memory) and from revision requests.
        var notes = state.Scratchpad.Count == 0
            ? "(no research notes yet)"
            : string.Join("\n\n", state.Scratchpad);

        var memory = state.MemoryFacts.Count == 0
            ? "(no long-term facts)"
            : string.Join("\n", state.MemoryFacts.Select(f => "- " + f));

        // On a revision the previous draft plus review notes and human feedback turn this
        // call into "fix this", not "start over".
        var prior = string.IsNullOrWhiteSpace(state.Draft)
            ? "(no previous draft)"
            : state.Draft;

        var feedback = state.HumanFeedback.Count == 0
            ? "(none)"
            : string.Join("\n", state.HumanFeedback.Select(f => "- " + f));

        var draft = await ChatHelper.CompleteAsync(
            client,
            "You are a writer agent. Produce the deliverable the user asked for. " +
            "Use the research notes for facts. Follow long-term preferences when they apply. " +
            "If a review or a human asked for changes, revise the previous draft. " +
            "Do not invent policy numbers that are not in the notes.",
            $"""
            Goal: {state.Goal}
            Instruction: {instruction}

            Long-term memory:
            {memory}

            Research notes (this run):
            {notes}

            Previous draft:
            {prior}

            Review notes: {state.ReviewNotes}
            Human feedback:
            {feedback}
            """);

        // STEP 2: Write back to state.
        // CRITICAL: reset the score. A stale "8" from the previous draft would otherwise
        // let this new, unreviewed text pass the review edge (§5).
        state.Draft = draft.Trim();
        state.ReviewScore = 0;
        state.Trace.Add($"[draft] wrote {state.Draft.Length} chars (revision {state.RevisionCount})");
        if (step is not null) step.Status = "done";
    }
}
