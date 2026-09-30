using System.Text.RegularExpressions;
using AIMLAPP.Learning.ProductionAiDemo.Security;

namespace AIMLAPP.Learning.ProductionAiDemo.Caching;

// Exact cache: same normalized question, same stored answer, no second model call.
// Questions that contain an email or an SSN are not stored.
// Three safety limits: a TTL, a maximum size, and no personal data. See 07-ProductionAI.md §4.
// Cache reads only. Never cache a tool with side effects, such as sending an email.
public sealed class AnswerCache
{
    private readonly int _maxEntries;
    private readonly Dictionary<string, Entry> _items = new(StringComparer.Ordinal);

    public int Hits { get; private set; }
    public int Misses { get; private set; }

    public AnswerCache(int maxEntries)
    {
        if (maxEntries < 1)
            throw new ArgumentOutOfRangeException(nameof(maxEntries));
        _maxEntries = maxEntries;
    }

    public bool TryGet(string question, DateTimeOffset now, out string answer)
    {
        var key = Normalize(question);
        // WHY a TTL: policies change. An old answer must expire instead of being served forever.
        if (_items.TryGetValue(key, out var entry) && entry.ExpiresAt > now)
        {
            Hits++;
            answer = entry.Answer;
            return true;
        }

        if (entry is not null)
            _items.Remove(key);

        Misses++;
        answer = "";
        return false;
    }

    public bool Set(string question, string answer, DateTimeOffset now, TimeSpan ttl)
    {
        // Personal data in the question would be stored next to the answer. Skip it.
        if (PiiRedactor.ContainsEmail(question) || PiiRedactor.ContainsSsn(question))
            return false;

        var key = Normalize(question);
        // Size bound: when full, drop the oldest entry. An unbounded dictionary is a
        // memory leak. Tune it in appsettings.json → ProductionAi.CacheMaxEntries.
        if (!_items.ContainsKey(key) && _items.Count >= _maxEntries)
        {
            var oldest = _items.OrderBy(pair => pair.Value.StoredAt).First().Key;
            _items.Remove(oldest);
        }

        _items[key] = new Entry
        {
            Answer = answer,
            ExpiresAt = now + ttl,
            StoredAt = now,
        };
        return true;
    }

    // "  How many   vacation days? " and "how many vacation days?" share one slot.
    // WHY normalize: without it, harmless case and spacing differences miss
    // the cache and pay for the same answer again.
    public static string Normalize(string question)
    {
        var collapsed = Regex.Replace(question.Trim().ToLowerInvariant(), @"\s+", " ");
        return collapsed;
    }

    private sealed class Entry
    {
        public required string Answer { get; init; }
        public required DateTimeOffset ExpiresAt { get; init; }
        public required DateTimeOffset StoredAt { get; init; }
    }
}
