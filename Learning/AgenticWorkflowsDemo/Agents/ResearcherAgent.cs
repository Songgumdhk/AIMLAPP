using OpenAI.Chat;
using AIMLAPP.Learning.AgenticWorkflowsDemo.Knowledge;
using AIMLAPP.Learning.AgenticWorkflowsDemo.Models;

namespace AIMLAPP.Learning.AgenticWorkflowsDemo.Agents;

// RESEARCH AGENT — calls the handbook tool, then writes notes onto the scratchpad.
// Those notes are short-term memory. The writer reads them later from the state;
// no model "remembers" them between calls. See 04-AgenticWorkflows.md §6 and §8.
// Narrow job: search and summarise. It never drafts, scores, or picks the next agent.
public static class ResearcherAgent
{
    public static async Task RunAsync(ChatClient client, WorkflowState state)
    {
        // The planner's instruction becomes the search query. Mode 5 has no plan, so it
        // falls back to the goal.
        var step = state.Plan.FirstOrDefault(s => s.Action == "research" && s.Status == "pending");
        var query = step?.Instruction ?? state.Goal;
        // The whole handbook is four short pages. Keep every page that matches
        // so a goal about "vacation and security" is not trimmed down to one topic.
        var sources = PolicyCorpus.Search(query, topK: 4);

        // The tool result itself is the memory. A summary can drop a number;
        // the passages cannot, because they are stored verbatim.
        state.Scratchpad.Add(sources);

        // WHY "verbatim" and "say so": the writer and reviewer both trust these notes,
        // so a paraphrased number or an invented fact here spreads through the whole run.
        var notes = await ChatHelper.CompleteAsync(
            client,
            "You are a research agent. Write 4 to 6 short factual bullet points. " +
            "Use only the handbook sources. Keep numbers, deadlines, and limits verbatim. " +
            "Cover every source you were given. If something was not in the handbook, say so.",
            $"Question:\n{query}\n\nHandbook sources:\n{sources}");

        state.Scratchpad.Add(notes.Trim());
        state.Trace.Add($"[research] added notes ({sources.Split("## ").Length - 1} source(s))");
        // Marking the step done is what lets the router move on to "draft".
        if (step is not null) step.Status = "done";
    }
}
