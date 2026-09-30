using System.Text.Json;

namespace AIMLAPP.Learning.AgenticWorkflowsDemo.Memory;

// LONG-TERM MEMORY — facts that survive after the process exits.
// Stored as JSON under the build output folder (bin/), which is gitignored.
// Short-term notes stay on WorkflowState.Scratchpad and are not written here.
// WHY explicit: nothing lands here automatically. Code calls Remember for a preference or
// a one-line history record, never a whole transcript. See 04-AgenticWorkflows.md §6.
public sealed class MemoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;

    public MemoryStore()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "agent-memory");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "facts.json");
    }

    public string FilePath => _path;

    public List<string> Load()
    {
        if (!File.Exists(_path)) return [];
        try
        {
            var facts = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(_path));
            return facts?.Where(f => !string.IsNullOrWhiteSpace(f)).ToList() ?? [];
        }
        catch
        {
            // A corrupt memory file should mean "no memory", not a crashed workflow.
            return [];
        }
    }

    public void Remember(string fact)
    {
        if (string.IsNullOrWhiteSpace(fact)) return;
        var facts = Load();
        // Skip duplicates so repeated runs do not bloat the facts every prompt will carry.
        if (facts.Contains(fact)) return;
        facts.Add(fact.Trim());
        File.WriteAllText(_path, JsonSerializer.Serialize(facts, JsonOptions));
    }

    public void ForgetAll()
    {
        if (File.Exists(_path))
            File.Delete(_path);
    }
}
