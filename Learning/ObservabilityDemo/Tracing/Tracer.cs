using System.Diagnostics;

namespace AIMLAPP.Learning.ObservabilityDemo.Tracing;

// In-memory tracer. A production app exports the same fields through
// OpenTelemetry. The shape is the lesson: a trace id, nested spans,
// a status, and attributes you chose to keep. See 06-Observability.md §2 and §7.
public sealed class Tracer
{
    // Elapsed time, not DateTime subtraction. A wall clock can jump.
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    // The span that is open on this async flow. Nested Begin() calls
    // read it as their parent, so callers do not pass parent ids around.
    private readonly AsyncLocal<string?> _current = new();
    private readonly List<TraceSpan> _spans = [];

    // One id for the whole piece of work, so a request can be found among thousands.
    public string TraceId { get; } = Guid.NewGuid().ToString("N")[..8];
    public IReadOnlyList<TraceSpan> Spans => _spans;
    public long ElapsedMs => _clock.ElapsedMilliseconds;

    public SpanScope Begin(string name, string kind)
    {
        var parent = _current.Value;
        var span = new TraceSpan
        {
            Id = Guid.NewGuid().ToString("N")[..6],
            ParentId = parent,
            Name = name,
            Kind = kind,
            StartMs = ElapsedMs,
        };
        _spans.Add(span);
        // This span is now the parent of whatever Begin() runs inside the using.
        _current.Value = span.Id;
        return new SpanScope(this, span, parent);
    }

    internal void Restore(string? parentId) => _current.Value = parentId;
}

// The handle for one open span. Used in a `using` block so the span always
// closes, even when the code inside throws. The Record* methods attach the
// facts you chose to keep: tokens, model, previews, and errors.
public sealed class SpanScope : IDisposable
{
    private readonly Tracer _tracer;
    private readonly TraceSpan _span;
    private readonly string? _parentId;
    private bool _ended;

    internal SpanScope(Tracer tracer, TraceSpan span, string? parentId)
    {
        _tracer = tracer;
        _span = span;
        _parentId = parentId;
    }

    public void SetDetail(string detail) => _span.Detail = detail;

    public void Fail(string error)
    {
        // Dispose does not change Status, so an error survives the end of the using.
        _span.Status = "error";
        _span.Error = error;
    }

    // Call only on the span that made the API call. Store the counts the API
    // returned, not an estimate, so the cost view reprices real numbers.
    public void RecordChat(string model, int inputTokens, int outputTokens, int cachedInputTokens)
    {
        _span.Model = model;
        _span.InputTokens = inputTokens;
        _span.OutputTokens = outputTokens;
        _span.CachedInputTokens = cachedInputTokens;
    }

    public void RecordEmbed(string model, int inputTokens)
    {
        _span.Model = model;
        _span.InputTokens = inputTokens;
        // Setting Model marks this span as a billable API call.
        // An embedding has no generated text, so output tokens stay 0.
        _span.OutputTokens = 0;
    }

    public void SetPromptPreview(string text) => _span.PromptPreview = Trim(text);

    public void SetOutputPreview(string text) => _span.OutputPreview = Trim(text);

    public void Dispose()
    {
        if (_ended) return;
        _ended = true;
        // Runs when the using block ends, including after an exception,
        // so the duration is recorded either way.
        _span.EndMs = _tracer.ElapsedMs;
        _tracer.Restore(_parentId);
    }

    // The trace keeps a short preview. A long stuffed prompt stays out of the console.
    // WHY a preview: enough to debug, while most of a prompt that holds secrets or
    // personal data stays out of logs. This demo's prompts are a public handbook.
    private static string Trim(string text)
    {
        var flat = text.ReplaceLineEndings(" ").Trim();
        return flat.Length <= 180 ? flat : flat[..180] + "...";
    }
}
