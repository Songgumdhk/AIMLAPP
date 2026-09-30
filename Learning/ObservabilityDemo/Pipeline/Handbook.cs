using OpenAI.Embeddings;
using AIMLAPP.Learning.ObservabilityDemo.Tracing;

namespace AIMLAPP.Learning.ObservabilityDemo.Pipeline;

public sealed record HandbookDoc(string Id, string Title, string Body);

public sealed record HandbookHit(string Id, string Title, string Body, double Score);

// Four short policies. They are embedded once per process, on their own trace,
// so a later question does not pay the ingest cost again (06-Observability.md §6).
public sealed class Handbook
{
    public static readonly IReadOnlyList<HandbookDoc> Docs =
    [
        new("hr-vacation", "Vacation Policy",
            "Full-time employees accrue 15 days of paid vacation per year in the first two years, then 20 days per year."),
        new("fin-expense", "Expense Policy EXP-75",
            "Policy EXP-75 reimburses meals up to $75 per person. A receipt is required over $25. Alcohol is not reimbursable."),
        new("sec-incident", "Security Hotline",
            "Report a suspected breach to the Security Operations Center at extension 9911."),
        new("it-password", "Password Reset",
            "Reset a password at https://login.acme.com. The link expires after 30 minutes. The helpdesk extension is 4200."),
    ];

    private float[][]? _vectors;

    public bool IsReady => _vectors is not null;

    public async Task IndexAsync(Tracer tracer, EmbeddingClient embeddings)
    {
        using var span = tracer.Begin("ingest.embed", "embed");
        var inputs = Docs.Select(d => d.Title + "\n" + d.Body).ToList();
        // One batched call for all documents: one span, one usage number to price.
        var response = await embeddings.GenerateEmbeddingsAsync(inputs);
        // Keep each vector lined up with Docs[i]. The API can return rows out of order.
        var ordered = response.Value.OrderBy(e => e.Index).ToList();
        if (ordered.Count != Docs.Count)
            throw new InvalidOperationException("Embedding count did not match the handbook.");

        // Embeddings bill input tokens only. The vector is not output text.
        var usage = response.Value.Usage;
        span.RecordEmbed(ModelNames.Embed, usage?.InputTokenCount ?? 0);
        span.SetDetail($"{Docs.Count} documents");
        _vectors = ordered.Select(e => e.ToFloats().ToArray()).ToArray();
    }

    // Brute-force search in memory. No API call, so the retrieve span has no tokens.
    public List<HandbookHit> Search(float[] query, int k)
    {
        if (_vectors is null)
            throw new InvalidOperationException("Index the handbook before search.");

        return Docs.Select((doc, i) => new HandbookHit(doc.Id, doc.Title, doc.Body, Cosine(query, _vectors[i])))
            .OrderByDescending(h => h.Score)
            .Take(k)
            .ToList();
    }

    // Same similarity as chapter 3. The + 1e-10 keeps a zero vector from dividing by 0.
    private static double Cosine(float[] a, float[] b)
    {
        double dot = 0, magA = 0, magB = 0;
        var n = Math.Min(a.Length, b.Length);
        for (var i = 0; i < n; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        return dot / (Math.Sqrt(magA) * Math.Sqrt(magB) + 1e-10);
    }
}
