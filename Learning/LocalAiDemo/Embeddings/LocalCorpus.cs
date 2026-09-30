namespace AIMLAPP.Learning.LocalAiDemo.Embeddings;

public sealed record LocalDoc(string Id, string Text);

// A handful of lines so local vectors have something to search.
// The query and the documents must be embedded by the same local model.
public static class LocalCorpus
{
    public static readonly IReadOnlyList<LocalDoc> Docs =
    [
        new("hr-vacation", "Full-time employees accrue 15 days of paid vacation per year in the first two years."),
        new("fin-expense", "Policy EXP-75 reimburses meals up to 75 dollars per person."),
        new("sec-incident", "Report a suspected breach to extension 9911."),
        new("office-snacks", "The kitchen stocks coffee tea and fruit on Monday mornings."),
    ];
}
