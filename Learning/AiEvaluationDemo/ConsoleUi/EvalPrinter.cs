using System.Globalization;
using System.Text.RegularExpressions;
using AIMLAPP.Learning.AiEvaluationDemo.Data;
using AIMLAPP.Learning.AiEvaluationDemo.Judges;
using AIMLAPP.Learning.AiEvaluationDemo.Metrics;
using AIMLAPP.Learning.AiEvaluationDemo.Models;

namespace AIMLAPP.Learning.AiEvaluationDemo.ConsoleUi;

// Console output for chapter 5. No scoring happens here; it only formats numbers that
// RetrievalMetrics, the judges, and HallucinationChecker.Summarize already computed.
public static class EvalPrinter
{
    public static void PrintRetrieval(RetrievalReport report)
    {
        var cases = LabeledSet.Cases;
        Console.WriteLine();
        Console.WriteLine($"Retrieval @ k={RetrievalMetrics.K}. Columns are Precision, Recall, Hit, Reciprocal rank.");
        Console.WriteLine("Naive returns the first 3 documents and never reads the question.");
        Console.WriteLine();
        const string cols = "P    R    Hit  RR   ";
        Console.WriteLine($"{"Case",-22} {"Naive",-20} {"Keyword",-20} {"Vector",-20}");
        Console.WriteLine($"{"",-22} {cols} {cols} {cols}");

        for (var i = 0; i < cases.Count; i++)
        {
            Console.WriteLine(
                $"{cases[i].Id,-22} {Cell(report.Naive[i])} {Cell(report.Keyword[i])} {Cell(report.Vector[i])}");
        }

        Console.WriteLine(
            $"{"MEAN",-22} {Cell(Mean(report.Naive))} {Cell(Mean(report.Keyword))} {Cell(Mean(report.Vector))}");

        PrintGap(report);
    }

    public static void PrintRagPath(
        EvalCase? evalCase,
        string question,
        IReadOnlyList<RetrievedHit> hits,
        RetrievalScore? retrieval,
        string answer,
        RagJudgement judgement,
        IReadOnlyList<ClaimVerdict> claims)
    {
        Console.WriteLine();
        Console.WriteLine("Question");
        Console.WriteLine("  " + question);
        Console.WriteLine("   |");
        Console.WriteLine($"   v  retrieved {hits.Count}");
        for (var i = 0; i < hits.Count; i++)
            Console.WriteLine($"  {i + 1}. {hits[i].DocId}  ({hits[i].Title})");

        Console.WriteLine("   |");
        Console.WriteLine("   v  were the correct documents retrieved?");
        if (evalCase is null || retrieval is null)
        {
            Console.WriteLine("  No gold labels for this question. Precision and recall need a labeled set.");
        }
        else
        {
            Console.WriteLine("  Gold: " + string.Join(", ", evalCase.RelevantDocIds));
            Console.WriteLine(
                $"  Hit@{RetrievalMetrics.K} = {Bit(retrieval.Hit)},  Precision = {Num(retrieval.PrecisionAtK)},  Recall = {Num(retrieval.RecallAtK)},  RR = {Num(retrieval.ReciprocalRank)}");
            Console.WriteLine("  " + evalCase.Note);
        }

        Console.WriteLine("   |");
        Console.WriteLine("   v  answer");
        foreach (var line in answer.Split('\n'))
            Console.WriteLine("  " + line.TrimEnd());

        Console.WriteLine("   |");
        Console.WriteLine("   v  did the answer use the context, and was it supported?");
        PrintMark("context relevance", judgement.ContextRelevance);
        PrintMark("answer relevance", judgement.AnswerRelevance);
        PrintMark("faithfulness", judgement.Faithfulness);
        Console.WriteLine();
        Console.WriteLine("Answer relevance can stay high when faithfulness drops. They are separate scores.");
        PrintClaims(claims);
    }

    public static void PrintClaims(IReadOnlyList<ClaimVerdict> claims)
    {
        Console.WriteLine();
        Console.WriteLine("Claims (supported by CONTEXT only — not by whether the answer was on topic):");
        if (claims.Count == 0)
        {
            Console.WriteLine("  (no claims parsed)");
            return;
        }

        foreach (var claim in claims)
        {
            Console.WriteLine($"  {claim.Verdict,-13} {claim.Text}");
            if (!string.IsNullOrWhiteSpace(claim.Evidence))
                Console.WriteLine($"  {"",-13} evidence: {Snip(claim.Evidence, 110)}");
        }

        var summary = HallucinationChecker.Summarize(claims);
        Console.WriteLine();
        Console.WriteLine(
            $"Hallucination rate = ({summary.Unsupported} unsupported + {summary.Contradicted} contradicted) / {summary.Supported + summary.Unsupported + summary.Contradicted} labeled claims = {Num(summary.Rate)}");
        if (summary.Unlabeled > 0)
            Console.WriteLine($"Unlabeled claims (left out of the rate): {summary.Unlabeled}");
    }

    public static void PrintRubricPair(string title, RagJudgement judgement)
    {
        Console.WriteLine();
        Console.WriteLine(title);
        PrintMark("context relevance", judgement.ContextRelevance);
        PrintMark("answer relevance", judgement.AnswerRelevance);
        PrintMark("faithfulness", judgement.Faithfulness);
    }

    public static void PrintGradeTable(
        IReadOnlyList<RetrievalScore> vector,
        IReadOnlyList<RagJudgement> judgements)
    {
        Console.WriteLine();
        Console.WriteLine($"{"Case",-22} {"Hit",4} {"Ctx",4} {"Ans",4} {"Faith",6}");
        for (var i = 0; i < LabeledSet.Cases.Count; i++)
        {
            var mark = judgements[i];
            Console.WriteLine(
                $"{LabeledSet.Cases[i].Id,-22} {Bit(vector[i].Hit),4} {Mark(mark.ContextRelevance),4} {Mark(mark.AnswerRelevance),4} {Mark(mark.Faithfulness),6}");
        }

        Console.WriteLine(
            $"{"MEAN",-22} {Num(vector.Average(s => s.Hit)),4} {Num(judgements.Average(j => ScoreOrSkip(j.ContextRelevance))),4} {Num(judgements.Average(j => ScoreOrSkip(j.AnswerRelevance))),4} {Num(judgements.Average(j => ScoreOrSkip(j.Faithfulness))),6}");
        Console.WriteLine();
        Console.WriteLine("Hit comes from gold document ids. The other three columns are judge scores.");
        Console.WriteLine("A 0 means that judge reply did not contain a usable score.");
    }

    // Points at the row where keyword and vector disagree most (recall + rank). That row
    // is where one retriever caught a paraphrase or exact token the other missed, which
    // is the argument for chapter 3's hybrid search.
    private static void PrintGap(RetrievalReport report)
    {
        var cases = LabeledSet.Cases;
        var index = 0;
        var best = -1.0;
        for (var i = 0; i < cases.Count; i++)
        {
            var gap = Math.Abs(report.Keyword[i].RecallAtK - report.Vector[i].RecallAtK)
                      + Math.Abs(report.Keyword[i].ReciprocalRank - report.Vector[i].ReciprocalRank);
            if (gap > best)
            {
                best = gap;
                index = i;
            }
        }

        Console.WriteLine();
        if (best <= 0.001)
        {
            Console.WriteLine("Keyword and vector agreed on recall and rank for every row.");
            Console.WriteLine("Read parental-paraphrase and locked-out anyway: keyword has no overlapping token with those gold documents.");
            index = IndexOf("parental-paraphrase");
        }
        else
        {
            Console.WriteLine("Largest keyword vs vector gap:");
        }

        var evalCase = cases[index];
        Console.WriteLine(evalCase.Id);
        Console.WriteLine(evalCase.Note);
        Console.WriteLine("Gold: " + string.Join(", ", evalCase.RelevantDocIds));
        Console.WriteLine("Keyword: " + ListIds(report.Keyword[index].RetrievedIds));
        Console.WriteLine("Vector:  " + ListIds(report.Vector[index].RetrievedIds));

        if (report.Keyword[index].RetrievedIds.Count < RetrievalMetrics.K)
        {
            Console.WriteLine(
                $"Keyword returned {report.Keyword[index].RetrievedIds.Count} document(s). Precision@{RetrievalMetrics.K} still divides by {RetrievalMetrics.K}, so empty slots count as misses.");
        }
    }

    private static void PrintMark(string name, RubricMark mark)
    {
        var score = mark.Score == 0 ? "unscored" : $"{mark.Score}/5";
        Console.WriteLine($"  {name}: {score}");
        if (!string.IsNullOrWhiteSpace(mark.Reason))
            Console.WriteLine("    " + mark.Reason.Trim());
    }

    private static string Cell(RetrievalScore score) =>
        $"{Num(score.PrecisionAtK),4} {Num(score.RecallAtK),4} {Num(score.Hit),4} {Num(score.ReciprocalRank),4} ";

    private static RetrievalScore Mean(IReadOnlyList<RetrievalScore> scores) =>
        new(
            scores.Average(s => s.PrecisionAtK),
            scores.Average(s => s.RecallAtK),
            scores.Average(s => s.Hit),
            scores.Average(s => s.ReciprocalRank),
            []);

    private static int IndexOf(string id)
    {
        for (var i = 0; i < LabeledSet.Cases.Count; i++)
            if (LabeledSet.Cases[i].Id == id) return i;
        return 0;
    }

    private static string ListIds(IReadOnlyList<string> ids) =>
        ids.Count == 0 ? "(none)" : string.Join(", ", ids);

    private static string Bit(double hit) => hit > 0 ? "1" : "0";

    private static string Mark(RubricMark mark) =>
        mark.Score == 0 ? "-" : mark.Score.ToString(CultureInfo.InvariantCulture);

    // Despite the name, nothing is skipped: a 0 ("judge failed") stays in the mean on
    // purpose, so a parse failure drags the average down visibly instead of vanishing (§8).
    private static double ScoreOrSkip(RubricMark mark) => mark.Score;

    private static string Num(double value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string Snip(string text, int max)
    {
        var flat = Regex.Replace(text.Trim(), @"\s+", " ");
        return flat.Length <= max ? flat : flat[..max] + "...";
    }
}
