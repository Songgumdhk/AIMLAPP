namespace AIMLAPP.Learning.ObservabilityDemo.Tracing;

// One step inside a trace. Tokens live only on the span that called a model.
// A parent such as "request" stays at zero so a later sum cannot bill the
// same call twice. See 06-Observability.md §2 for the field table.
public sealed class TraceSpan
{
    public required string Id { get; init; }
    // Null means a root span. ParentId is what turns a flat list into a tree.
    public string? ParentId { get; init; }
    public required string Name { get; init; }
    // embed, internal, tool, llm, or server. Lets a viewer group spans by what they do.
    public required string Kind { get; init; }
    // Milliseconds from the tracer's Stopwatch, not wall-clock timestamps.
    public long StartMs { get; init; }
    public long EndMs { get; set; }
    public string Status { get; set; } = "ok";
    public string? Error { get; set; }
    public string? Model { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    // A subset of InputTokens, not extra tokens. Kept so you can see prompt-cache reuse.
    public int CachedInputTokens { get; set; }
    public string Detail { get; set; } = "";
    // Short previews, not full text. A full prompt can hold secrets or personal data.
    public string PromptPreview { get; set; } = "";
    public string OutputPreview { get; set; } = "";

    public long DurationMs => Math.Max(0, EndMs - StartMs);
    // True only when RecordChat or RecordEmbed stored a model name.
    public bool IsModelCall => Model is not null;
}
