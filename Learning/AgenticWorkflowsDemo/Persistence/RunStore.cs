using System.Text.Json;
using AIMLAPP.Learning.AgenticWorkflowsDemo.Models;

namespace AIMLAPP.Learning.AgenticWorkflowsDemo.Persistence;

// STATE PERSISTENCE — write the whole WorkflowState after every node.
// A human-in-the-loop pause is just a run whose Status is "waiting_for_human".
// Resume = load that JSON and continue the graph from CurrentNode.
// WHY: a crash or Ctrl+C costs at most one node, and a resume does not pay again for the
// plan and research calls. Files live in agent-runs/ under bin/. See 04-AgenticWorkflows.md §9.
public sealed class RunStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _dir;

    public RunStore()
    {
        _dir = Path.Combine(AppContext.BaseDirectory, "agent-runs");
        Directory.CreateDirectory(_dir);
    }

    public string DirectoryPath => _dir;

    // Checkpoints store state, not console output. If the next node needs a field,
    // it must be on WorkflowState before this call.
    public void Save(WorkflowState state)
    {
        var path = Path.Combine(_dir, state.RunId + ".json");
        File.WriteAllText(path, JsonSerializer.Serialize(state, JsonOptions));
    }

    public WorkflowState? Load(string runId)
    {
        var path = Path.Combine(_dir, runId + ".json");
        if (!File.Exists(path)) return null;
        var state = JsonSerializer.Deserialize<WorkflowState>(File.ReadAllText(path));
        if (state is null) return null;
        // An older or hand-edited file may carry nulls; nodes assume these lists exist.
        state.Plan ??= [];
        state.Scratchpad ??= [];
        state.MemoryFacts ??= [];
        state.HumanFeedback ??= [];
        state.Trace ??= [];
        return state;
    }

    public IReadOnlyList<WorkflowState> List()
    {
        if (!Directory.Exists(_dir)) return [];
        var runs = new List<WorkflowState>();
        foreach (var file in Directory.GetFiles(_dir, "*.json"))
        {
            try
            {
                var state = JsonSerializer.Deserialize<WorkflowState>(File.ReadAllText(file));
                if (state is not null) runs.Add(state);
            }
            catch
            {
                // Skip a corrupt checkpoint rather than failing the menu.
            }
        }
        return runs.OrderByDescending(r => r.RunId).ToList();
    }
}
