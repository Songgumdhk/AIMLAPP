namespace AIMLAPP.Learning.AgenticWorkflowsDemo.Knowledge;

// A tiny handbook the research agent can search.
// Keyword overlap on purpose: this chapter is about the workflow, not embeddings.
// Chapter 3 already taught hybrid search. Here retrieval is just a tool the graph calls.
// Swap in Chapter 3's retriever later if you want real RAG inside the research node (§8).
public static class PolicyCorpus
{
    private sealed record Doc(string Title, string Body);

    private static readonly Doc[] Docs =
    [
        new("Vacation policy",
            "Full-time employees accrue 15 vacation days per year. " +
            "Up to 5 unused days may carry over into the next calendar year if the manager approves by December 1. " +
            "New hires may take vacation after 90 days. " +
            "HR confirms carry-over in writing. Do not assume carry-over was approved."),
        new("Security policy",
            "New engineers complete security training before their first production access. " +
            "The training deadline is the Friday of the first week. " +
            "Laptops use full-disk encryption. VPN is required off the office network. " +
            "Badge access to the server room is requested by the manager, not by the new hire."),
        new("Engineering onboarding",
            "Each new engineer is assigned a mentor for the first 30 days. " +
            "The mentor sets up the repo, the VPN profile, and the on-call shadow rota. " +
            "The first week is local-only: no production deploys until security training is marked complete."),
        new("Tools and access",
            "Standard toolchain is .NET SDK, Git, and the internal NuGet feed. " +
            "Source control access is granted on day one. " +
            "Production credentials are granted only after security training and mentor sign-off."),
    ];

    // Score = number of shared words. Deterministic, free, and easy to debug, so any odd
    // behaviour in the run comes from the graph or the prompts, not from retrieval.
    // Returns one string with "## Title" headers; the researcher stores it verbatim.
    public static string Search(string query, int topK = 2)
    {
        var terms = Tokenize(query);
        var ranked = Docs
            .Select(doc => (doc, Score: Tokenize(doc.Title + " " + doc.Body).Count(t => terms.Contains(t))))
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.doc.Title)
            .Take(topK)
            .ToList();

        // An explicit "nothing matched" lets the researcher say so instead of inventing facts.
        if (ranked.Count == 0 || ranked[0].Score == 0)
            return "(no handbook passage matched)";

        return string.Join("\n\n", ranked.Select(x => $"## {x.doc.Title}\n{x.doc.Body}"));
    }

    // Words of 1-2 letters ("a", "of", "to") are dropped so they do not inflate the overlap.
    private static HashSet<string> Tokenize(string text) =>
        text.ToLowerInvariant()
            .Split([' ', '.', ',', ';', ':', '-', '/', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 2)
            .ToHashSet();
}
