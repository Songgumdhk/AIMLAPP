using OpenAI.Chat;
using AIMLAPP.Learning.AgenticWorkflowsDemo.Knowledge;
using AIMLAPP.Learning.AgenticWorkflowsDemo.Models;

namespace AIMLAPP.Learning.AgenticWorkflowsDemo.Agents;

// REVIEWER AGENT — scores the draft. The graph, not the reviewer, decides
// whether to loop back to the writer. The reviewer only writes a score and notes.
// See BriefWorkflow.ApplyReview and 04-AgenticWorkflows.md §8.
public static class ReviewerAgent
{
    public static async Task RunAsync(ChatClient client, WorkflowState state)
    {
        // No draft means nothing to grade. Skip the model call instead of paying for a guess.
        if (string.IsNullOrWhiteSpace(state.Draft))
        {
            state.ReviewScore = 0;
            state.ReviewNotes = "No draft to review.";
            state.Trace.Add("[review] score=0 — no draft");
            return;
        }

        var notes = state.Scratchpad.Count == 0
            ? "(no research notes)"
            : string.Join("\n\n", state.Scratchpad);

        // WHY the research notes: the reviewer checks faithfulness against the same facts the
        // writer had, not against its own general knowledge.
        // Note the prompt asks for a score, not a verdict. Pass/fail is decided in code.
        var raw = await ChatHelper.CompleteAsync(
            client,
            "You are a reviewer agent. Score the draft from 1 to 10. " +
            "Pass only if it answers the goal and stays faithful to the research notes. " +
            "Output only JSON like {\"score\":8,\"notes\":\"which facts are present or missing\"}. " +
            "The notes must describe this draft.",
            $"Goal: {state.Goal}\n\nResearch notes:\n{notes}\n\nDraft:\n{state.Draft}",
            json: true);

        // Fallback first, then overwrite if the JSON is usable. A below-pass default means a
        // broken review triggers a rewrite (bounded by MaxRevisions) rather than a silent pass.
        state.ReviewScore = 5;
        state.ReviewNotes = "Could not parse the review. Treating the score as 5 so the loop stays visible.";

        using (var doc = ChatHelper.ParseOrNull(raw))
        {
            // Clamp to 1-10: a model answering 0 or 42 must not break the threshold comparison.
            if (doc is not null && doc.RootElement.TryGetProperty("score", out var scoreEl) && TryReadScore(scoreEl, out var score))
                state.ReviewScore = Math.Clamp(score, 1, 10);
            if (doc is not null && doc.RootElement.TryGetProperty("notes", out var notesEl))
                state.ReviewNotes = notesEl.GetString() ?? state.ReviewNotes;
        }

        state.Trace.Add($"[review] score={state.ReviewScore} — {state.ReviewNotes}");
    }

    // Models sometimes return 7.5 instead of 7. Accept both.
    private static bool TryReadScore(System.Text.Json.JsonElement scoreEl, out int score)
    {
        if (scoreEl.TryGetInt32(out score)) return true;
        if (scoreEl.TryGetDouble(out var asDouble))
        {
            score = (int)Math.Round(asDouble);
            return true;
        }
        score = 0;
        return false;
    }
}