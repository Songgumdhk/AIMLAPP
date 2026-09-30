namespace AIMLAPP.Learning.ProductionAiDemo.RateLimit;

// Fixed window per caller. Checked before the model, so a loop stops
// spending tokens from chapter 6 instead of stopping after the bill arrives.
// A web app keeps one limiter per user or API key. See 07-ProductionAI.md §5.
// The caller passes `now`, so the demo can use a fake clock instead of waiting.
public sealed class WindowLimiter
{
    private readonly int _limit;
    private readonly TimeSpan _window;
    private readonly Queue<DateTimeOffset> _hits = new();

    public WindowLimiter(int limit, TimeSpan window)
    {
        if (limit < 1) throw new ArgumentOutOfRangeException(nameof(limit));
        _limit = limit;
        _window = window;
    }

    public int Limit => _limit;

    public bool TryAcquire(DateTimeOffset now)
    {
        DropExpired(now);
        // A rejected attempt is not recorded, so it does not use up the next window.
        if (_hits.Count >= _limit)
            return false;

        _hits.Enqueue(now);
        return true;
    }

    public int Remaining(DateTimeOffset now)
    {
        DropExpired(now);
        return Math.Max(0, _limit - _hits.Count);
    }

    // Forget calls older than one window. Once they age out, capacity comes back.
    private void DropExpired(DateTimeOffset now)
    {
        while (_hits.Count > 0 && now - _hits.Peek() >= _window)
            _hits.Dequeue();
    }
}
